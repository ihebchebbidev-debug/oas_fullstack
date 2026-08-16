using System.Collections.Generic;
using System.Linq;

namespace MyApi.Modules.Plugins
{
    /// <summary>
    /// Minimal mirror of the frontend manifest registry — used for server-side
    /// validation (core-locking, dependency checks, total-count for stats).
    /// Keep codes in sync with src/modules/{module}/plugin.ts.
    /// </summary>
    public static class KnownPlugins
    {
        public record Entry(string Code, bool IsCore, string[] Dependencies);

        // Codes are immutable. Order is irrelevant. Add new plugins below.
        // MUST stay 1:1 with src/modules/*/plugin.ts (41 entries).
        public static readonly IReadOnlyList<Entry> All = new List<Entry>
        {
            // ── Core / System (cannot be disabled) ──
            new("PL0033SYSTEM",        true,  new string[0]),
            new("PL0034SETTINGS",      true,  new string[0]),
            new("PL0035AUTH",          true,  new string[0]),
            new("PL0036DASHBOARD",     true,  new string[0]),

            // ── CRM ──
            new("PL0001CONTACTS",      false, new[] { "PL0037LOOKUPS" }),
            new("PL0002SALES",         false, new[] { "PL0001CONTACTS", "PL0007ARTICLES", "PL0037LOOKUPS" }),
            new("PL0003DEALS",         false, new[] { "PL0001CONTACTS", "PL0037LOOKUPS" }),
            new("PL0004PROJECTS",      false, new[] { "PL0001CONTACTS", "PL0037LOOKUPS" }),
            new("PL0004INVOICES",      false, new[] { "PL0001CONTACTS", "PL0002SALES", "PL0007ARTICLES", "PL0037LOOKUPS" }),
            new("PL0005OFFERS",        false, new[] { "PL0001CONTACTS", "PL0007ARTICLES", "PL0037LOOKUPS" }),
            new("PL0006SUPPORT",       false, new[] { "PL0037LOOKUPS" }),

            // ── Inventory & Stock ──
            new("PL0007ARTICLES",      false, new[] { "PL0037LOOKUPS" }),
            new("PL0008INVSERVICES",   false, new[] { "PL0007ARTICLES", "PL0037LOOKUPS" }),
            new("PL0009STOCK",         false, new[] { "PL0007ARTICLES", "PL0037LOOKUPS" }),

            // ── Calendar / Tasks / Documents ──
            new("PL0010CALENDAR",      false, new string[0]),
            new("PL0011TASKS",         false, new[] { "PL0037LOOKUPS" }),
            new("PL0012DOCUMENTS",     false, new string[0]),

            // ── HR ──
            new("PL0013HR",            false, new[] { "PL0037LOOKUPS" }),
            new("PL0014SKILLS",        false, new string[0]),

            // ── Field ──
            new("PL0015FIELD",         false, new[] { "PL0001CONTACTS", "PL0007ARTICLES", "PL0037LOOKUPS" }),
            new("PL0024DISPATCHER",    false, new[] { "PL0015FIELD", "PL0037LOOKUPS" }),
            new("PL0023SCHEDULING",    false, new[] { "PL0015FIELD", "PL0024DISPATCHER", "PL0037LOOKUPS" }),
            new("PL0018INSTALLATIONS", false, new[] { "PL0001CONTACTS", "PL0015FIELD", "PL0007ARTICLES", "PL0037LOOKUPS" }),

            // ── Finance / Purchases / Payments ──
            new("PL0025PURCHASES",     false, new[] { "PL0001CONTACTS", "PL0007ARTICLES", "PL0037LOOKUPS" }),
            new("PL0026PAYMENTS",      false, new[] { "PL0004INVOICES", "PL0037LOOKUPS" }),

            // ── Comms ──
            new("PL0027COMMUNICATION", false, new string[0]),
            new("PL0028EMAILCALENDAR", false, new string[0]),
            new("PL0029NOTIFICATIONS", false, new string[0]),
            new("PL0030EXTERNAL",      false, new string[0]),

            // ── Workflow / Forms / Lookups ──
            new("PL0031WORKFLOW",      false, new string[0]),
            new("PL0032DYNAMICFORMS",  false, new string[0]),
            new("PL0037LOOKUPS",       false, new string[0]),

            // ── Builders ──
            // Website and Dashboard builders removed per request

            // ── Analytics / Audit / Sync ──
            new("PL0040ANALYTICS",     false, new string[0]),
            new("PL0046REPORTING",     false, new string[0]),
            new("PL0041AIASSISTANT",   false, new string[0]),
            new("PL0042AUTOMATION",    false, new string[0]),
            new("PL0043USERS",         false, new string[0]),
            new("PL0044PREFERENCES",   true,  new string[0]),
            new("PL0045ONBOARDING",    true,  new string[0]),
        };

        public static readonly Dictionary<string, Entry> ByCode =
            All.ToDictionary(e => e.Code, e => e);

        public static bool IsCore(string code) =>
            ByCode.TryGetValue(code, out var e) && e.IsCore;

        public static bool Exists(string code) => ByCode.ContainsKey(code);

        public static IEnumerable<Entry> Dependents(string code) =>
            All.Where(e => e.Dependencies.Contains(code));

        /// <summary>Everything <paramref name="code"/> needs, transitively.</summary>
        public static List<string> TransitiveDependencies(string code)
        {
            var acc = new List<string>();
            var seen = new HashSet<string>();
            void Walk(string c)
            {
                if (!ByCode.TryGetValue(c, out var e)) return;
                foreach (var dep in e.Dependencies)
                {
                    if (!seen.Add(dep)) continue;
                    acc.Add(dep);
                    Walk(dep);
                }
            }
            Walk(code);
            acc.Remove(code);
            return acc;
        }

        /// <summary>Everything that breaks when <paramref name="code"/> goes off, transitively.</summary>
        public static List<string> TransitiveDependents(string code)
        {
            var acc = new List<string>();
            var seen = new HashSet<string>();
            void Walk(string c)
            {
                foreach (var dep in Dependents(c))
                {
                    if (!seen.Add(dep.Code)) continue;
                    acc.Add(dep.Code);
                    Walk(dep.Code);
                }
            }
            Walk(code);
            acc.Remove(code);
            return acc;
        }
    }
}
