using System;

namespace MyApi.Infrastructure;

/// <summary>
/// Marks an <see cref="ITenantEntity"/> as belonging to a logical module
/// (e.g. "contacts", "articles"). The module's scope is read from
/// <c>ModuleScopeSettings</c> and controls how the global query filter and
/// SaveChanges stamp resolve TenantId:
///
///   shared      → TenantId is forced to 0 (single shared dataset)
///   per_company → TenantId is the request's current company (default)
///
/// Entities WITHOUT this attribute keep the original per-company behaviour.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class ModuleScopeAttribute : Attribute
{
    public string ModuleKey { get; }
    public ModuleScopeAttribute(string moduleKey)
    {
        if (string.IsNullOrWhiteSpace(moduleKey))
            throw new ArgumentException("Module key is required.", nameof(moduleKey));
        ModuleKey = moduleKey.Trim().ToLowerInvariant();
    }
}
