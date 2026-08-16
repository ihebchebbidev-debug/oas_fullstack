using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MyApi.Modules.Settings.Models;

namespace MyApi.Modules.Settings.Services
{
    /// <summary>
    /// Resolves the data-scope (shared / per_company) for a logical module key.
    /// One instance per DbContext (per tenant DB, per request) — cached for the
    /// lifetime of the context so global query filters resolve synchronously.
    /// </summary>
    public interface IModuleScopeProvider
    {
        bool IsShared(string moduleKey);
        IReadOnlyDictionary<string, string> GetAll();
        void Invalidate();
    }

    /// <summary>
    /// Default implementation. Lazy-loads from the ModuleScopeSettings table
    /// on first call. Failures (e.g. table not migrated yet) degrade to
    /// "per_company for everything" so the app never breaks.
    /// </summary>
    public sealed class ModuleScopeProvider : IModuleScopeProvider
    {
        private readonly DbContext _db;
        private Dictionary<string, string>? _cache;
        private readonly object _lock = new();

        public ModuleScopeProvider(DbContext db) { _db = db; }

        private Dictionary<string, string> Load()
        {
            if (_cache != null) return _cache;
            lock (_lock)
            {
                if (_cache != null) return _cache;
                try
                {
                    var rows = _db.Set<ModuleScopeSetting>()
                        .AsNoTracking()
                        .ToList();
                    _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var r in rows)
                        _cache[r.ModuleKey] = r.Scope ?? "per_company";
                }
                catch
                {
                    // Table missing (pre-migration) → safe default.
                    _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                return _cache;
            }
        }

        public bool IsShared(string moduleKey)
        {
            if (string.IsNullOrWhiteSpace(moduleKey)) return false;
            var map = Load();
            return map.TryGetValue(moduleKey, out var scope) &&
                   string.Equals(scope, "shared", StringComparison.OrdinalIgnoreCase);
        }

        public IReadOnlyDictionary<string, string> GetAll() => Load();

        public void Invalidate()
        {
            lock (_lock) { _cache = null; }
        }
    }
}
