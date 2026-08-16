using Microsoft.AspNetCore.Http;

namespace MyApi.Modules.OAS.Common.Scope;

/// <summary>
/// Site/zone/line scope check applied before every OAS read (spec §4.1,
/// §8.0). A user with no scope_* claims sees everything within their
/// tenant (typical for admin/supervisor); a scoped operator only sees data
/// under their assigned site/zone/line.
///
/// This is the exact-match layer only. Hierarchy containment (a user scoped
/// to a *site* must also see its zones/lines/posts) requires resolving the
/// hierarchy tree and is added once oas_sites/zones/lines/posts exist
/// (Lot 3) — sub-modules built before then have nothing to scope by site/
/// zone/line yet, so this interface is the stable contract they code
/// against without waiting on Hierarchy to land first.
/// </summary>
public interface IOasScopeFilter
{
    bool IsSiteInScope(Guid? currentScopeSiteId, Guid? currentScopeZoneId, Guid? currentScopeLineId, Guid siteId);
    bool IsZoneInScope(Guid? currentScopeSiteId, Guid? currentScopeZoneId, Guid? currentScopeLineId, Guid zoneId, Guid parentSiteId);
    bool IsLineInScope(Guid? currentScopeSiteId, Guid? currentScopeZoneId, Guid? currentScopeLineId, Guid lineId, Guid parentZoneId, Guid parentSiteId);
}

public class OasScopeFilter : IOasScopeFilter
{
    public bool IsSiteInScope(Guid? scopeSiteId, Guid? scopeZoneId, Guid? scopeLineId, Guid siteId)
    {
        if (scopeSiteId is null && scopeZoneId is null && scopeLineId is null) return true; // unrestricted
        return scopeSiteId == siteId;
    }

    public bool IsZoneInScope(Guid? scopeSiteId, Guid? scopeZoneId, Guid? scopeLineId, Guid zoneId, Guid parentSiteId)
    {
        if (scopeSiteId is null && scopeZoneId is null && scopeLineId is null) return true;
        if (scopeZoneId is not null) return scopeZoneId == zoneId;
        if (scopeSiteId is not null) return scopeSiteId == parentSiteId;
        return false;
    }

    public bool IsLineInScope(Guid? scopeSiteId, Guid? scopeZoneId, Guid? scopeLineId, Guid lineId, Guid parentZoneId, Guid parentSiteId)
    {
        if (scopeSiteId is null && scopeZoneId is null && scopeLineId is null) return true;
        if (scopeLineId is not null) return scopeLineId == lineId;
        if (scopeZoneId is not null) return scopeZoneId == parentZoneId;
        if (scopeSiteId is not null) return scopeSiteId == parentSiteId;
        return false;
    }
}
