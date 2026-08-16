using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using MyApi.Data;
using Npgsql;

namespace MyApi.Infrastructure;

/// <summary>
/// Detects model columns which are absent from an existing tenant database and
/// applies only safe, additive repairs. It never drops or changes a column.
/// Full schema evolution must still use reviewed migrations.
/// </summary>
public sealed class DatabaseSchemaSynchronizer
{
    private const long AdvisoryLockId = 4_606_873_001;
    private readonly ILogger<DatabaseSchemaSynchronizer> _logger;

    public DatabaseSchemaSynchronizer(ILogger<DatabaseSchemaSynchronizer> logger)
    {
        _logger = logger;
    }

    public async Task<SchemaSyncResult> SynchronizeAsync(
        string databaseKey,
        string connectionString,
        DbContextOptions<ApplicationDbContext> options,
        bool repair,
        CancellationToken cancellationToken = default)
    {
        var repaired = new List<string>();
        var unresolved = new List<string>();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var lockCommand = new NpgsqlCommand("SELECT pg_advisory_lock(@id)", connection);
        lockCommand.Parameters.AddWithValue("id", AdvisoryLockId);
        await lockCommand.ExecuteNonQueryAsync(cancellationToken);

        try
        {
            await using var db = new ApplicationDbContext(options);
            var model = db.Model;
            var sql = model.GetRelationalModel();

            foreach (var table in sql.Tables.OrderBy(t => t.Schema).ThenBy(t => t.Name))
            {
                var schema = table.Schema ?? "public";
                if (!await TableExistsAsync(connection, schema, table.Name, cancellationToken))
                {
                    var createSql = BuildCreateTableSql(schema, table);
                    if (createSql == null || !repair)
                    {
                        unresolved.Add($"{schema}.{table.Name} (table)");
                        continue;
                    }

                    try
                    {
                        await using var createCommand = new NpgsqlCommand(createSql, connection) { CommandTimeout = 120 };
                        await createCommand.ExecuteNonQueryAsync(cancellationToken);
                        repaired.Add($"{schema}.{table.Name} (table)");
                        _logger.LogWarning("Schema drift repaired on database {Database}: created table {Table}", databaseKey, $"{schema}.{table.Name}");
                    }
                    catch (Exception ex)
                    {
                        unresolved.Add($"{schema}.{table.Name} (table)");
                        _logger.LogError(ex, "Could not auto-create table {Table} on database {Database}", $"{schema}.{table.Name}", databaseKey);
                    }
                    continue;
                }

                var actualColumns = await GetColumnsAsync(connection, schema, table.Name, cancellationToken);
                foreach (var column in table.Columns.OrderBy(c => c.Name))
                {
                    if (actualColumns.Contains(column.Name)) continue;

                    var identifier = $"{schema}.{table.Name}.{column.Name}";
                    if (!repair)
                    {
                        unresolved.Add(identifier);
                        continue;
                    }

                    var definition = BuildColumnDefinition(column, isCreateTable: false);
                    var commandText = $"ALTER TABLE {Quote(schema)}.{Quote(table.Name)} ADD COLUMN IF NOT EXISTS {Quote(column.Name)} {definition}";
                    try
                    {
                        await using var command = new NpgsqlCommand(commandText, connection) { CommandTimeout = 60 };
                        await command.ExecuteNonQueryAsync(cancellationToken);
                        repaired.Add(identifier);
                        _logger.LogWarning("Schema drift repaired on database {Database}: added {Column}", databaseKey, identifier);
                    }
                    catch (Exception ex)
                    {
                        // Last resort: add the column as plain nullable so queries stop failing.
                        try
                        {
                            var fallback = $"ALTER TABLE {Quote(schema)}.{Quote(table.Name)} ADD COLUMN IF NOT EXISTS {Quote(column.Name)} {column.StoreType} NULL";
                            await using var fallbackCommand = new NpgsqlCommand(fallback, connection) { CommandTimeout = 60 };
                            await fallbackCommand.ExecuteNonQueryAsync(cancellationToken);
                            repaired.Add(identifier + " (nullable fallback)");
                            _logger.LogWarning(ex, "Added {Column} as nullable on database {Database} after the typed repair failed", identifier, databaseKey);
                        }
                        catch (Exception fallbackEx)
                        {
                            unresolved.Add(identifier);
                            _logger.LogError(fallbackEx, "Could not auto-add column {Column} on database {Database}", identifier, databaseKey);
                        }
                    }
                }

            }
        }
        finally
        {
            await using var unlockCommand = new NpgsqlCommand("SELECT pg_advisory_unlock(@id)", connection);
            unlockCommand.Parameters.AddWithValue("id", AdvisoryLockId);
            await unlockCommand.ExecuteNonQueryAsync(CancellationToken.None);
        }

        return new SchemaSyncResult(databaseKey, repaired, unresolved);
    }

    /// <summary>
    /// Builds a CREATE TABLE statement from the EF relational model, including the
    /// primary key. Foreign keys and indexes are intentionally left to migrations.
    /// </summary>
    private static string? BuildCreateTableSql(string schema, ITable table)
    {
        if (!table.Columns.Any()) return null;

        var lines = table.Columns
            .OrderBy(c => c.Name)
            .Select(c => $"  {Quote(c.Name)} {BuildColumnDefinition(c, isCreateTable: true)}")
            .ToList();

        var primaryKey = table.PrimaryKey;
        if (primaryKey != null && primaryKey.Columns.Count > 0)
        {
            var keyColumns = string.Join(", ", primaryKey.Columns.Select(c => Quote(c.Name)));
            lines.Add($"  CONSTRAINT {Quote(primaryKey.Name ?? $"PK_{table.Name}")} PRIMARY KEY ({keyColumns})");
        }

        return $"CREATE TABLE IF NOT EXISTS {Quote(schema)}.{Quote(table.Name)} (\n{string.Join(",\n", lines)}\n)";
    }

    /// <summary>
    /// Always returns a usable definition: explicit model defaults win, otherwise a
    /// neutral zero-value default is used so NOT NULL columns can be added to
    /// tables which already contain rows. Nothing is ever dropped or narrowed.
    /// </summary>
    private static string BuildColumnDefinition(IColumn column, bool isCreateTable)
    {
        var storeType = column.StoreType;
        var nullSuffix = column.IsNullable ? string.Empty : " NOT NULL";
        var property = column.PropertyMappings.Select(m => m.Property).FirstOrDefault();

        if (property != null)
        {
            var computedSql = property.GetComputedColumnSql();
            if (!string.IsNullOrWhiteSpace(computedSql))
                return $"{storeType} GENERATED ALWAYS AS ({computedSql}) STORED";

            var defaultSql = property.GetDefaultValueSql();
            if (!string.IsNullOrWhiteSpace(defaultSql))
                return $"{storeType} DEFAULT {defaultSql}{nullSuffix}";

            var defaultValue = property.GetDefaultValue();
            if (defaultValue != null)
            {
                var mapping = property.GetRelationalTypeMapping();
                return $"{storeType} DEFAULT {mapping.GenerateSqlLiteral(defaultValue)}{nullSuffix}";
            }

            // Store-generated keys: keep them generated instead of defaulting to 0.
            if (property.ValueGenerated == ValueGenerated.OnAdd)
            {
                var clr = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                var store = storeType.ToLowerInvariant();
                if (clr == typeof(int) || clr == typeof(long) || clr == typeof(short))
                    return $"{storeType} GENERATED BY DEFAULT AS IDENTITY{nullSuffix}";
                if (clr == typeof(Guid) || store.Contains("uuid"))
                    return $"{storeType} DEFAULT gen_random_uuid(){nullSuffix}";
            }
        }

        if (column.IsNullable) return storeType;

        var neutralDefault = NeutralDefaultFor(property?.ClrType, storeType);
        if (neutralDefault != null)
            return $"{storeType} DEFAULT {neutralDefault} NOT NULL";

        // Unknown shape: never block the repair, just relax nullability.
        // A reviewed migration can tighten it later.
        return isCreateTable ? $"{storeType} NULL" : $"{storeType} NULL";
    }

    private static string? NeutralDefaultFor(Type? clrType, string storeType)
    {
        var type = clrType == null ? null : Nullable.GetUnderlyingType(clrType) ?? clrType;
        var store = storeType.ToLowerInvariant();

        // Store type wins for text-like columns: enums and value converters are
        // frequently persisted as strings, where a numeric zero would be invalid.
        if (store.Contains("char") || store.Contains("text")) return "''";
        if (store.Contains("json")) return store.Contains("jsonb") ? "'{}'::jsonb" : "'{}'::json";
        if (store.Contains("bool")) return "FALSE";
        if (store.Contains("uuid")) return "'00000000-0000-0000-0000-000000000000'::uuid";
        if (store.Contains("bytea")) return "'\\x'::bytea";
        if (store.StartsWith("time") && !store.Contains("timestamp")) return "'00:00:00'";
        if (store.Contains("interval")) return "'0 seconds'::interval";
        if (store.Contains("timestamp") || store.Contains("date")) return "NOW()";
        if (store.Contains("int") || store.Contains("numeric") || store.Contains("decimal")
            || store.Contains("money") || store.Contains("real") || store.Contains("double")
            || store.Contains("serial")) return "0";
        if (store.EndsWith("[]")) return $"'{{}}'::{store}";

        if (type == typeof(bool)) return "FALSE";
        if (type == typeof(Guid)) return "'00000000-0000-0000-0000-000000000000'::uuid";
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return "NOW()";
        if (type == typeof(TimeSpan)) return "'00:00:00'";
        if (type == typeof(string)) return "''";
        if (type == typeof(byte[])) return "'\\x'::bytea";
        if (type != null && type.IsEnum) return "0";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
            || type == typeof(decimal) || type == typeof(double) || type == typeof(float))
            return "0";

        return null;
    }



    private static async Task<bool> TableExistsAsync(
        NpgsqlConnection connection, string schema, string table, CancellationToken cancellationToken)
    {
        const string query = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = @schema AND table_name = @table)";
        await using var command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", table);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task<HashSet<string>> GetColumnsAsync(
        NpgsqlConnection connection, string schema, string table, CancellationToken cancellationToken)
    {
        const string query = "SELECT column_name FROM information_schema.columns WHERE table_schema = @schema AND table_name = @table";
        await using var command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", table);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken)) columns.Add(reader.GetString(0));
        return columns;
    }

    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";
}

public sealed record SchemaSyncResult(
    string DatabaseKey,
    IReadOnlyList<string> RepairedColumns,
    IReadOnlyList<string> UnresolvedColumns)
{
    public bool IsHealthy => UnresolvedColumns.Count == 0;
}