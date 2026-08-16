using MyApi.Data;
using MyApi.Modules.Numbering.DTOs;
using MyApi.Modules.Numbering.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace MyApi.Modules.Numbering.Services
{
    public class NumberingService : INumberingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NumberingService> _logger;

        // Valid entities
        private static readonly HashSet<string> ValidEntities = new(StringComparer.OrdinalIgnoreCase)
        {
            "Offer", "Sale", "ServiceOrder", "Dispatch", "Deal",
            // Purchases module: PurchaseOrderService / GoodsReceiptService /
            // SupplierInvoiceService all call GetNextAsync(...) with these keys.
            // Without them the service throws ArgumentException on every Create*
            // call — callers catch and fall back to a GUID, but that loses the
            // configurable numbering template and pollutes order numbers.
            "PurchaseOrder", "GoodsReceipt", "SupplierInvoice",
            // Customer invoice ledger (Phase B). InvoiceService.PostAsync calls
            // GetNextAsync("Invoice"); without this entry it hits the catch
            // block and falls back to a raw "INV-yyyyMMdd-{id}" string, losing
            // the configurable numbering template.
            "Invoice"
        };

        // Entity prefixes for legacy fallback
        private static readonly Dictionary<string, string> EntityPrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Offer", "OFR" },
            { "Sale", "SALE" },
            { "ServiceOrder", "SO" },
            { "Dispatch", "DISP" },
            { "Deal", "DEAL" },
            { "PurchaseOrder", "PO" },
            { "GoodsReceipt", "GR" },
            { "SupplierInvoice", "INV" },
            { "Invoice", "INV" }
        };

        // Token regex: matches {TOKEN} or {TOKEN:param}
        private static readonly Regex TokenRegex = new(@"\{([A-Z_]+)(?::([^}]+))?\}", RegexOptions.Compiled);

        // Supported tokens
        private static readonly HashSet<string> SupportedTokens = new()
        {
            "YYYY", "YEAR", "YY", "DATE", "SEQ", "GUID", "TS", "ENTITY", "ID"
        };

        public NumberingService(ApplicationDbContext context, ILogger<NumberingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ──────────────────────────────────────────────────────────
        // GetNextAsync — atomic number generation
        // ──────────────────────────────────────────────────────────
        public async Task<string> GetNextAsync(string entity)
        {
            if (!ValidEntities.Contains(entity))
                throw new ArgumentException($"Unknown entity: {entity}");

            var settings = await _context.Set<NumberingSettings>()
                .FirstOrDefaultAsync(s => s.EntityName == entity);

            // Fix §2.1: legacy fallback path must also verify uniqueness. Two
            // concurrent creates while numbering is disabled could otherwise
            // produce identical GUID-prefixed numbers (birthday-paradox on 6 hex
            // chars = 1 collision per ~4k rows/day is small but non-zero and has
            // been observed on bulk-imports).
            if (settings == null || !settings.IsEnabled)
            {
                for (int i = 0; i < 5; i++)
                {
                    var candidate = GenerateLegacy(entity);
                    if (!await DocumentNumberExistsAsync(entity, candidate))
                        return candidate;
                    _logger.LogWarning("Legacy number {Number} collision for {Entity}, retrying (attempt {Retry}/5)", candidate, entity, i + 1);
                }
                // Ultimate: append extra entropy
                var prefixLegacy = EntityPrefixes.GetValueOrDefault(entity, entity);
                return $"{prefixLegacy}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString("N")[..10].ToUpper()}";
            }

            int retries = 0;
            const int maxRetries = 10;

            while (retries < maxRetries)
            {
                try
                {
                    // Refresh timestamp on each attempt so {TS}/{DATE}/{GUID} tokens change
                    var now = DateTime.UtcNow;
                    var result = await RenderTemplateWithSequence(settings, now, consume: true);

                    // Verify uniqueness against the actual table to prevent collisions with legacy data
                    if (!await DocumentNumberExistsAsync(entity, result))
                        return result;

                    retries++;
                    _logger.LogWarning("Generated number {Number} already exists for {Entity}, retrying (attempt {Retry}/{Max})", result, entity, retries, maxRetries);

                    // Brief delay for timestamp-based strategies to get a new second
                    if (settings.Strategy is "timestamp_random")
                        await Task.Delay(100);
                }
                catch (DbUpdateConcurrencyException)
                {
                    retries++;
                    _logger.LogWarning("Numbering concurrency conflict for {Entity}, retry {Retry}/{Max}", entity, retries, maxRetries);
                    await Task.Delay(retries * 50);
                }
            }

            // Ultimate fallback: use entity prefix + timestamp + GUID for guaranteed uniqueness
            var prefix = EntityPrefixes.GetValueOrDefault(entity, entity);
            var fallback = $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
            _logger.LogWarning("Numbering exhausted all retries for {Entity}, using guaranteed-unique fallback: {Number}", entity, fallback);
            return fallback;
        }

        /// <summary>
        /// Check if a document number already exists in the corresponding entity table.
        /// </summary>
        private async Task<bool> DocumentNumberExistsAsync(string entity, string number)
        {
            try
            {
                return entity switch
                {
                    "Offer" => await _context.Offers.AnyAsync(o => o.OfferNumber == number),
                    "Sale" => await _context.Sales.AnyAsync(s => s.SaleNumber == number),
                    "ServiceOrder" => await _context.ServiceOrders.AnyAsync(s => s.OrderNumber == number),
                    "Dispatch" => await _context.Dispatches.AnyAsync(d => d.DispatchNumber == number),
                    "Deal" => await _context.Deals.AnyAsync(d => d.DealNumber == number),
                    "PurchaseOrder" => await _context.PurchaseOrders.AnyAsync(o => o.OrderNumber == number),
                    "GoodsReceipt" => await _context.GoodsReceipts.AnyAsync(r => r.ReceiptNumber == number),
                    "SupplierInvoice" => await _context.SupplierInvoices.AnyAsync(i => i.InvoiceNumber == number),
                    "Invoice" => await _context.Invoices.AnyAsync(i => i.InvoiceNumber == number),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check document number uniqueness for {Entity}, assuming unique", entity);
                return false;
            }
        }

        // ──────────────────────────────────────────────────────────
        // PreviewAsync — preview using stored settings
        // ──────────────────────────────────────────────────────────
        public async Task<List<string>> PreviewAsync(string entity, int count = 5)
        {
            var settings = await _context.Set<NumberingSettings>()
                .FirstOrDefaultAsync(s => s.EntityName == entity);

            if (settings == null || !settings.IsEnabled)
            {
                // For legacy preview, generate safe non-throwing previews
                return Enumerable.Range(1, count).Select(i => GenerateLegacyPreview(entity, i)).ToList();
            }

            return PreviewFromSettings(settings, count);
        }

        // ──────────────────────────────────────────────────────────
        // PreviewFromTemplate — ad-hoc preview for admin UI
        // ──────────────────────────────────────────────────────────
        public List<string> PreviewFromTemplate(NumberingPreviewRequest request)
        {
            var fakeSettings = new NumberingSettings
            {
                EntityName = request.Entity,
                Template = request.Template,
                Strategy = request.Strategy,
                ResetFrequency = request.ResetFrequency,
                StartValue = request.StartValue,
                Padding = request.Padding,
                IsEnabled = true,
            };

            return PreviewFromSettings(fakeSettings, Math.Min(request.Count, 10));
        }

        // ──────────────────────────────────────────────────────────
        // ValidateTemplate
        // ──────────────────────────────────────────────────────────
        public (bool IsValid, List<string> Errors, List<string> Warnings) ValidateTemplate(string template, string strategy)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (string.IsNullOrWhiteSpace(template))
            {
                errors.Add("Template cannot be empty.");
                return (false, errors, warnings);
            }

            if (template.Length > 200)
                errors.Add("Template must be 200 characters or less.");

            var matches = TokenRegex.Matches(template);
            bool hasSeq = false;
            bool hasUnique = false;

            foreach (Match m in matches)
            {
                var token = m.Groups[1].Value;
                if (!SupportedTokens.Contains(token))
                {
                    errors.Add($"Unknown token: {{{token}}}");
                }
                if (token == "SEQ") hasSeq = true;
                if (token is "SEQ" or "GUID" or "TS" or "ID") hasUnique = true;
            }

            if (!hasUnique)
            {
                warnings.Add("Template has no unique token ({SEQ}, {GUID}, {TS}, or {ID}). Numbers will be identical — a GUID suffix will be auto-appended if collisions occur.");
            }

            if (hasSeq && strategy is "timestamp_random" or "guid")
                warnings.Add("{SEQ} token detected — a sequential counter will be used automatically regardless of strategy selection.");

            if (!hasSeq && strategy is "db_sequence" or "atomic_counter")
                warnings.Add("Strategy uses sequences but template has no {SEQ} token. Consider adding {SEQ} for sequential numbering.");

            // Warn about timestamp-only templates (same-second collisions)
            bool hasTs = matches.Cast<Match>().Any(m => m.Groups[1].Value == "TS");
            if (hasTs && !hasSeq && !matches.Cast<Match>().Any(m => m.Groups[1].Value == "GUID"))
                warnings.Add("Template uses only {TS} for uniqueness. Concurrent requests within the same second may collide — consider adding {GUID} or {SEQ}.");

            return (errors.Count == 0, errors, warnings);
        }

        // ──────────────────────────────────────────────────────────
        // Settings CRUD
        // ──────────────────────────────────────────────────────────
        public async Task<List<NumberingSettingsDto>> GetAllSettingsAsync()
        {
            var all = await _context.Set<NumberingSettings>().ToListAsync();

            // Auto-seed if table is empty (first run before migration seed or EF seed runs)
            if (all.Count == 0)
            {
                await EnsureDefaultSettingsAsync();
                all = await _context.Set<NumberingSettings>().ToListAsync();
            }

            // Ensure all 4 entities are represented
            var result = new List<NumberingSettingsDto>();
            foreach (var entity in ValidEntities)
            {
                var existing = all.FirstOrDefault(s => s.EntityName == entity);
                result.Add(existing != null ? MapToDto(existing) : GetDefaultDto(entity));
            }
            return result;
        }

        /// <summary>
        /// Ensures default NumberingSettings rows exist for all entities.
        /// Called on first access if the table is empty — safe for concurrent calls via ON CONFLICT.
        /// </summary>
        private async Task EnsureDefaultSettingsAsync()
        {
            foreach (var entity in ValidEntities)
            {
                var exists = await _context.Set<NumberingSettings>()
                    .AnyAsync(s => s.EntityName == entity);

                if (!exists)
                {
                    var defaults = GetDefaultDto(entity);
                    _context.Set<NumberingSettings>().Add(new NumberingSettings
                    {
                        EntityName = entity,
                        IsEnabled = false,
                        Template = defaults.Template,
                        Strategy = defaults.Strategy,
                        ResetFrequency = defaults.ResetFrequency,
                        StartValue = defaults.StartValue,
                        Padding = defaults.Padding,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    });
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Race condition — another instance seeded first. Safe to ignore.
                _logger.LogInformation("NumberingSettings already seeded by another instance");
            }
        }

        public async Task<NumberingSettingsDto?> GetSettingsAsync(string entity)
        {
            var s = await _context.Set<NumberingSettings>()
                .FirstOrDefaultAsync(x => x.EntityName == entity);
            return s != null ? MapToDto(s) : GetDefaultDto(entity);
        }

        public async Task<NumberingSettingsDto> SaveSettingsAsync(string entity, UpdateNumberingSettingsRequest request)
        {
            if (!ValidEntities.Contains(entity))
                throw new ArgumentException($"Unknown entity: {entity}");

            var (isValid, errors, _) = ValidateTemplate(request.Template, request.Strategy);
            if (!isValid)
                throw new InvalidOperationException($"Invalid template: {string.Join("; ", errors)}");

            // Fix §11.4: wrap settings upsert + AutoSync in one transaction so
            // enabling {SEQ} numbering can never leave IsEnabled=true with a stale
            // counter (which was the very race the auto-sync exists to prevent,
            // just shifted one step later).
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                var settings = await _context.Set<NumberingSettings>()
                    .FirstOrDefaultAsync(s => s.EntityName == entity);

                if (settings == null)
                {
                    settings = new NumberingSettings
                    {
                        EntityName = entity,
                        CreatedAt = DateTime.UtcNow,
                    };
                    _context.Set<NumberingSettings>().Add(settings);
                }

                settings.IsEnabled = request.IsEnabled;
                settings.Template = request.Template;
                settings.Strategy = request.Strategy;
                settings.ResetFrequency = request.ResetFrequency;
                settings.StartValue = request.StartValue;
                settings.Padding = request.Padding;
                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                if (request.IsEnabled && request.Template.Contains("{SEQ", StringComparison.OrdinalIgnoreCase))
                {
                    await AutoSyncSequenceCounterAsync(entity, request.ResetFrequency, request.StartValue);
                }

                await tx.CommitAsync();
                return MapToDto(settings);
            });
        }

        /// <summary>
        /// Ensures the sequence counter for an entity is ahead of existing document count.
        /// Called automatically when user enables/changes numbering settings.
        /// </summary>
        private async Task AutoSyncSequenceCounterAsync(string entity, string resetFrequency, int startValue)
        {
            try
            {
                var now = DateTime.UtcNow;
                var periodKey = GetPeriodKey(resetFrequency, now);

                // Fix §2.2: cover ALL valid entities, not just the original 5.
                // Previously PurchaseOrder / GoodsReceipt / SupplierInvoice / Invoice
                // fell through to `_ => 0`, so safeFloor was always 100 regardless of
                // real volume, causing collisions when {SEQ} numbering was enabled on
                // a table that already had >100 records.
                long existingCount = entity switch
                {
                    "Offer" => await _context.Offers.CountAsync(),
                    "Sale" => await _context.Sales.CountAsync(),
                    "ServiceOrder" => await _context.ServiceOrders.CountAsync(),
                    "Dispatch" => await _context.Dispatches.CountAsync(),
                    "Deal" => await _context.Deals.CountAsync(),
                    "PurchaseOrder" => await _context.PurchaseOrders.CountAsync(),
                    "GoodsReceipt" => await _context.GoodsReceipts.CountAsync(),
                    "SupplierInvoice" => await _context.SupplierInvoices.CountAsync(),
                    "Invoice" => await _context.Invoices.CountAsync(),
                    _ => 0
                };

                // Add buffer of 100 to safely skip any gaps/deleted records
                long safeFloor = Math.Max(existingCount + 100, startValue);

                // Only update if current counter is below safe floor
                var sql = @"
                    INSERT INTO ""NumberSequences"" (entity_name, period_key, last_value, created_at, updated_at)
                    VALUES ({0}, {1}, {2}, NOW(), NOW())
                    ON CONFLICT (entity_name, period_key)
                    DO UPDATE SET last_value = GREATEST(""NumberSequences"".last_value, {2}), updated_at = NOW()
                    RETURNING last_value;";

                await _context.Database.SqlQueryRaw<long>(sql, entity, periodKey, safeFloor).ToListAsync();
                _logger.LogInformation("Auto-synced sequence counter for {Entity} (period {Period}) to at least {Floor}", entity, periodKey, safeFloor);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to auto-sync sequence counter for {Entity}, numbering will still work via collision detection", entity);
                throw; // Re-raise so the outer SaveSettingsAsync transaction rolls back — was §11.4 silent-swallow bug.
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Render a template, optionally consuming a sequence counter atomically.
        /// </summary>
        private async Task<string> RenderTemplateWithSequence(NumberingSettings settings, DateTime now, bool consume)
        {
            long seqValue = settings.StartValue;

            // Always consume a sequence value when {SEQ} is in the template, regardless of strategy.
            // This ensures sequential numbering even if user picks "guid" strategy but includes {SEQ}.
            bool hasSeqToken = settings.Template.Contains("{SEQ", StringComparison.OrdinalIgnoreCase);

            if (consume && hasSeqToken)
            {
                seqValue = await GetNextSequenceValueAsync(settings.EntityName, settings.ResetFrequency, settings.StartValue, now);
            }

            return RenderTemplate(settings, now, seqValue);
        }

        /// <summary>
        /// Atomically increment and return the next sequence value.
        /// Uses UPDATE ... SET last_value = last_value + 1 RETURNING last_value via raw SQL for atomicity.
        /// </summary>
        private async Task<long> GetNextSequenceValueAsync(string entity, string resetFrequency, int startValue, DateTime now)
        {
            var periodKey = GetPeriodKey(resetFrequency, now);

            // Try atomic UPDATE + RETURNING first (PostgreSQL)
            var sql = @"
                INSERT INTO ""NumberSequences"" (entity_name, period_key, last_value, created_at, updated_at)
                VALUES ({0}, {1}, {2}, NOW(), NOW())
                ON CONFLICT (entity_name, period_key)
                DO UPDATE SET last_value = ""NumberSequences"".last_value + 1, updated_at = NOW()
                RETURNING last_value;";

            var result = await _context.Database
                .SqlQueryRaw<long>(sql, entity, periodKey, startValue)
                .ToListAsync();

            return result.FirstOrDefault();
        }

        private static string GetPeriodKey(string resetFrequency, DateTime now)
        {
            return resetFrequency switch
            {
                "yearly" => now.Year.ToString(),
                "monthly" => $"{now.Year}-{now.Month:D2}",
                _ => "all"
            };
        }

        /// <summary>
        /// Render template tokens into a final string.
        /// </summary>
        private string RenderTemplate(NumberingSettings settings, DateTime now, long seqValue)
        {
            var result = TokenRegex.Replace(settings.Template, match =>
            {
                var token = match.Groups[1].Value;
                var param = match.Groups[2].Success ? match.Groups[2].Value : null;

                return token switch
                {
                    "YYYY" or "YEAR" => now.Year.ToString(),
                    "YY" => now.ToString("yy"),
                    "DATE" => now.ToString(param ?? "yyyyMMdd"),
                    "SEQ" =>
                        seqValue.ToString().PadLeft(
                            param != null && int.TryParse(param, out var p) ? p : settings.Padding, '0'),
                    "GUID" =>
                        Guid.NewGuid().ToString("N")[..(param != null && int.TryParse(param, out var g) ? Math.Min(g, 32) : 8)].ToUpper(),
                    "TS" => now.ToString(param ?? "yyyyMMddHHmmss"),
                    "ENTITY" => EntityPrefixes.GetValueOrDefault(settings.EntityName, settings.EntityName),
                    "ID" => "0", // placeholder — actual DB id not known at generation time
                    _ => match.Value // leave unknown tokens as-is
                };
            });

            return result;
        }

        /// <summary>
        /// Preview N values using settings without consuming counters.
        /// </summary>
        private List<string> PreviewFromSettings(NumberingSettings settings, int count)
        {
            var now = DateTime.UtcNow;
            var results = new List<string>();

            for (int i = 0; i < count; i++)
            {
                long seqValue = settings.StartValue + i;
                results.Add(RenderTemplate(settings, now, seqValue));
            }

            return results;
        }

        /// <summary>
        /// Legacy number generation (current hardcoded logic).
        /// </summary>
        private string GenerateLegacy(string entity)
        {
            var now = DateTime.UtcNow;
            return entity switch
            {
                // Offer legacy requires DB query for next sequence — throw so caller (OfferService) uses its own DB-aware fallback
                "Offer" => throw new NotSupportedException("Offer legacy numbering requires DB query; use OfferService fallback"),
                "Sale" => $"SALE-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..5].ToUpper()}",
                "ServiceOrder" => $"SO-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                "Dispatch" => $"DISP-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
                "Deal" => $"DEAL-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..5].ToUpper()}",
                _ => $"DOC-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..4].ToUpper()}"
            };
        }

        /// <summary>
        /// Safe preview-only legacy generation (never throws, used for PreviewAsync).
        /// </summary>
        private static string GenerateLegacyPreview(string entity, int index)
        {
            var now = DateTime.UtcNow;
            return entity switch
            {
                "Offer" => $"OFR-{now.Year}-{index:D6}",
                "Sale" => $"SALE-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..5].ToUpper()}",
                "ServiceOrder" => $"SO-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                "Dispatch" => $"DISP-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
                "Deal" => $"DEAL-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..5].ToUpper()}",
                _ => $"DOC-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..4].ToUpper()}"
            };
        }

        private static NumberingSettingsDto MapToDto(NumberingSettings s) => new()
        {
            Id = s.Id,
            EntityName = s.EntityName,
            IsEnabled = s.IsEnabled,
            Template = s.Template,
            Strategy = s.Strategy,
            ResetFrequency = s.ResetFrequency,
            StartValue = s.StartValue,
            Padding = s.Padding,
            UpdatedAt = s.UpdatedAt,
        };

        /// <summary>
        /// Return default (unsaved) DTO matching current hardcoded logic.
        /// </summary>
        private static NumberingSettingsDto GetDefaultDto(string entity) => entity switch
        {
            "Offer" => new() { EntityName = "Offer", Template = "OFR-{YEAR}-{SEQ:6}", Strategy = "atomic_counter", ResetFrequency = "yearly", StartValue = 1, Padding = 6 },
            "Sale" => new() { EntityName = "Sale", Template = "SALE-{DATE:yyyyMMdd}-{GUID:5}", Strategy = "guid", ResetFrequency = "never", StartValue = 1, Padding = 5 },
            "ServiceOrder" => new() { EntityName = "ServiceOrder", Template = "SO-{DATE:yyyyMMdd}-{GUID:6}", Strategy = "guid", ResetFrequency = "never", StartValue = 1, Padding = 6 },
            "Dispatch" => new() { EntityName = "Dispatch", Template = "DISP-{TS:yyyyMMddHHmmss}", Strategy = "timestamp_random", ResetFrequency = "never", StartValue = 1, Padding = 6 },
            "Deal" => new() { EntityName = "Deal", Template = "DEAL-{YEAR}-{SEQ:5}", Strategy = "atomic_counter", ResetFrequency = "yearly", StartValue = 1, Padding = 5 },
            _ => new() { EntityName = entity, Template = $"{entity}-{{SEQ:6}}", Strategy = "atomic_counter", ResetFrequency = "yearly", StartValue = 1, Padding = 6 }
        };
    }
}
