using Microsoft.EntityFrameworkCore;
using MyApi.Modules.OAS.Common;
using MyApi.Modules.OAS.Hierarchy.DTOs;
using MyApi.Modules.OAS.Hierarchy.Models;

namespace MyApi.Modules.OAS.Hierarchy.Services;

public class OasHierarchyService : IOasHierarchyService
{
    private readonly OasDbContext _db;

    public OasHierarchyService(OasDbContext db) => _db = db;

    // ---- Sites ----------------------------------------------------------
    public async Task<IReadOnlyList<OasSiteDto>> GetSitesAsync(int tenantId)
    {
        // Materialize entities via EF (translated to SQL), THEN map to DTO
        // in-memory — a bare method-group in .Select() risks either
        // resolving to client-side Enumerable.Select (losing IQueryable,
        // caught at compile time) or, if forced into an expression tree,
        // failing at runtime because EF can't translate an arbitrary C#
        // method call to SQL. Two explicit steps sidesteps both.
        var sites = await _db.Set<OasSite>().OrderBy(s => s.Name).ToListAsync();
        return sites.Select(ToSiteDto).ToList();
    }

    public async Task<OasSiteDto> CreateSiteAsync(int tenantId, OasSiteRequestDto request)
    {
        var site = new OasSite { TenantId = tenantId, Code = request.Code, Name = request.Name, Timezone = request.Timezone ?? "Africa/Tunis", Address = request.Address };
        _db.Set<OasSite>().Add(site);
        await _db.SaveChangesAsync();
        return ToSiteDto(site);
    }

    public async Task<bool> UpdateSiteAsync(int tenantId, Guid id, OasSiteRequestDto request)
    {
        var site = await _db.Set<OasSite>().FindAsync(id);
        if (site is null) return false;
        site.Code = request.Code; site.Name = request.Name;
        if (request.Timezone is not null) site.Timezone = request.Timezone;
        site.Address = request.Address;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>v15 decision: cascades to zones/lines/posts in one transaction — a non-cascading archive leaves live children under a dead parent.</summary>
    public async Task<bool> ArchiveSiteAsync(int tenantId, Guid id)
    {
        var site = await _db.Set<OasSite>().FindAsync(id);
        if (site is null) return false;

        var now = DateTimeOffset.UtcNow;
        var zoneIds = await _db.Set<OasZone>().Where(z => z.SiteId == id).Select(z => z.Id).ToListAsync();
        var lineIds = await _db.Set<OasLine>().Where(l => zoneIds.Contains(l.ZoneId)).Select(l => l.Id).ToListAsync();

        await _db.Set<OasPost>().Where(p => lineIds.Contains(p.LineId)).ExecuteUpdateAsync(s => s.SetProperty(p => p.ArchivedAt, now).SetProperty(p => p.IsDeleted, true));
        await _db.Set<OasLine>().Where(l => zoneIds.Contains(l.ZoneId)).ExecuteUpdateAsync(s => s.SetProperty(l => l.ArchivedAt, now).SetProperty(l => l.IsDeleted, true));
        await _db.Set<OasZone>().Where(z => z.SiteId == id).ExecuteUpdateAsync(s => s.SetProperty(z => z.ArchivedAt, now).SetProperty(z => z.IsDeleted, true));

        site.ArchivedAt = now;
        site.IsDeleted = true;
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Zones ------------------------------------------------------------
    public async Task<IReadOnlyList<OasZoneDto>> GetZonesAsync(int tenantId, Guid? siteId)
    {
        var q = _db.Set<OasZone>().AsQueryable();
        if (siteId is not null) q = q.Where(z => z.SiteId == siteId);
        var zones = await q.OrderBy(z => z.SortOrder).ToListAsync();
        return zones.Select(ToZoneDto).ToList();
    }

    public async Task<OasZoneDto> CreateZoneAsync(int tenantId, OasZoneRequestDto request)
    {
        var zone = new OasZone { TenantId = tenantId, SiteId = request.SiteId, Code = request.Code, Name = request.Name, SortOrder = request.SortOrder };
        _db.Set<OasZone>().Add(zone);
        await _db.SaveChangesAsync();
        return ToZoneDto(zone);
    }

    public async Task<bool> UpdateZoneAsync(int tenantId, Guid id, OasZoneRequestDto request)
    {
        var zone = await _db.Set<OasZone>().FindAsync(id);
        if (zone is null) return false;
        zone.SiteId = request.SiteId; zone.Code = request.Code; zone.Name = request.Name; zone.SortOrder = request.SortOrder;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ArchiveZoneAsync(int tenantId, Guid id)
    {
        var zone = await _db.Set<OasZone>().FindAsync(id);
        if (zone is null) return false;

        var now = DateTimeOffset.UtcNow;
        var lineIds = await _db.Set<OasLine>().Where(l => l.ZoneId == id).Select(l => l.Id).ToListAsync();
        await _db.Set<OasPost>().Where(p => lineIds.Contains(p.LineId)).ExecuteUpdateAsync(s => s.SetProperty(p => p.ArchivedAt, now).SetProperty(p => p.IsDeleted, true));
        await _db.Set<OasLine>().Where(l => l.ZoneId == id).ExecuteUpdateAsync(s => s.SetProperty(l => l.ArchivedAt, now).SetProperty(l => l.IsDeleted, true));

        zone.ArchivedAt = now;
        zone.IsDeleted = true;
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Lines --------------------------------------------------------------
    public async Task<IReadOnlyList<OasLineDto>> GetLinesAsync(int tenantId, Guid? zoneId)
    {
        var q = _db.Set<OasLine>().AsQueryable();
        if (zoneId is not null) q = q.Where(l => l.ZoneId == zoneId);
        var lines = await q.OrderBy(l => l.SortOrder).ToListAsync();
        return lines.Select(ToLineDto).ToList();
    }

    public async Task<OasLineDto> CreateLineAsync(int tenantId, OasLineRequestDto request)
    {
        var line = new OasLine { TenantId = tenantId, ZoneId = request.ZoneId, Code = request.Code, Name = request.Name, SortOrder = request.SortOrder, TargetOee = request.TargetOee };
        _db.Set<OasLine>().Add(line);
        await _db.SaveChangesAsync();
        return ToLineDto(line);
    }

    public async Task<bool> UpdateLineAsync(int tenantId, Guid id, OasLineRequestDto request)
    {
        var line = await _db.Set<OasLine>().FindAsync(id);
        if (line is null) return false;
        line.ZoneId = request.ZoneId; line.Code = request.Code; line.Name = request.Name; line.SortOrder = request.SortOrder; line.TargetOee = request.TargetOee;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ArchiveLineAsync(int tenantId, Guid id)
    {
        var line = await _db.Set<OasLine>().FindAsync(id);
        if (line is null) return false;

        var now = DateTimeOffset.UtcNow;
        await _db.Set<OasPost>().Where(p => p.LineId == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.ArchivedAt, now).SetProperty(p => p.IsDeleted, true));

        line.ArchivedAt = now;
        line.IsDeleted = true;
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Posts --------------------------------------------------------------
    public async Task<IReadOnlyList<OasPostDto>> GetPostsAsync(int tenantId, Guid? lineId)
    {
        var q = _db.Set<OasPost>().AsQueryable();
        if (lineId is not null) q = q.Where(p => p.LineId == lineId);
        var posts = await q.OrderBy(p => p.SortOrder).ToListAsync();
        return posts.Select(ToPostDto).ToList();
    }

    public async Task<OasPostDto?> GetPostAsync(int tenantId, Guid id)
    {
        var post = await _db.Set<OasPost>().FindAsync(id);
        return post is null ? null : ToPostDto(post);
    }

    public async Task<OasPostDto?> GetPostByCodeAsync(int tenantId, string code)
    {
        var post = await _db.Set<OasPost>().FirstOrDefaultAsync(p => p.Code == code);
        return post is null ? null : ToPostDto(post);
    }

    public async Task<OasPostDto> CreatePostAsync(int tenantId, OasPostRequestDto request)
    {
        var post = new OasPost
        {
            TenantId = tenantId,
            LineId = request.LineId,
            Code = request.Code,
            Name = request.Name,
            SortOrder = request.SortOrder,
            PostType = request.PostType,
            QrToken = Guid.NewGuid().ToString("N"),
        };
        _db.Set<OasPost>().Add(post);
        await _db.SaveChangesAsync();
        return ToPostDto(post);
    }

    public async Task<bool> UpdatePostAsync(int tenantId, Guid id, OasPostRequestDto request)
    {
        var post = await _db.Set<OasPost>().FindAsync(id);
        if (post is null) return false;
        post.LineId = request.LineId; post.Code = request.Code; post.Name = request.Name; post.SortOrder = request.SortOrder; post.PostType = request.PostType;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdatePostAttributesAsync(int tenantId, Guid id, OasPostAttributesRequestDto request)
    {
        var post = await _db.Set<OasPost>().FindAsync(id);
        if (post is null) return false;
        if (request.PostType is not null) post.PostType = request.PostType;
        if (request.SortOrder is not null) post.SortOrder = request.SortOrder.Value;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetPostCriticalAsync(int tenantId, Guid id, bool isCritical)
    {
        var post = await _db.Set<OasPost>().FindAsync(id);
        if (post is null) return false;
        post.IsCritical = isCritical;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ArchivePostAsync(int tenantId, Guid id)
    {
        var post = await _db.Set<OasPost>().FindAsync(id);
        if (post is null) return false;
        post.ArchivedAt = DateTimeOffset.UtcNow;
        post.IsDeleted = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetPostCapacityAsync(int tenantId, Guid id)
    {
        // operators_required from the current cadence (oas_routings) — read
        // via raw SQL: Cadences' EF entity is a sibling sub-module, no
        // circular project reference needed for one scalar column.
        var value = await _db.Database.SqlQueryRaw<int?>(
            """select operators_required as "Value" from oas_routings where post_id = {0} order by updated_at desc limit 1""",
            id).FirstOrDefaultAsync();
        return value ?? 1;
    }

    // ---- Tree / resolution ----------------------------------------------------
    public async Task<IReadOnlyList<OasHierarchyTreeSiteDto>> GetTreeAsync(int tenantId)
    {
        var sites = await _db.Set<OasSite>().OrderBy(s => s.Name).ToListAsync();
        var zones = await _db.Set<OasZone>().OrderBy(z => z.SortOrder).ToListAsync();
        var lines = await _db.Set<OasLine>().OrderBy(l => l.SortOrder).ToListAsync();
        var posts = await _db.Set<OasPost>().OrderBy(p => p.SortOrder).ToListAsync();

        return sites.Select(s => new OasHierarchyTreeSiteDto
        {
            Id = s.Id,
            Code = s.Code,
            Name = s.Name,
            Zones = zones.Where(z => z.SiteId == s.Id).Select(z => new OasHierarchyTreeZoneDto
            {
                Id = z.Id,
                Code = z.Code,
                Name = z.Name,
                Lines = lines.Where(l => l.ZoneId == z.Id).Select(l => new OasHierarchyTreeLineDto
                {
                    Id = l.Id,
                    Code = l.Code,
                    Name = l.Name,
                    Posts = posts.Where(p => p.LineId == l.Id).Select(ToPostDto).ToList(),
                }).ToList(),
            }).ToList(),
        }).ToList();
    }

    // ---- QR badges --------------------------------------------------------------
    public async Task<OasQrTokenDto?> GetPostQrTokenAsync(int tenantId, Guid postId)
    {
        var post = await _db.Set<OasPost>().FindAsync(postId);
        return post is null ? null : new OasQrTokenDto { PostId = post.Id, Token = post.QrToken };
    }

    public async Task<OasQrTokenDto?> RotatePostQrTokenAsync(int tenantId, Guid postId)
    {
        var post = await _db.Set<OasPost>().FindAsync(postId);
        if (post is null) return null;
        post.QrToken = Guid.NewGuid().ToString("N");
        post.QrRotatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return new OasQrTokenDto { PostId = post.Id, Token = post.QrToken };
    }

    public async Task<IReadOnlyList<OasQrTokenDto>> GetQrTokensForLineAsync(int tenantId, Guid? lineId)
    {
        var q = _db.Set<OasPost>().AsQueryable();
        if (lineId is not null) q = q.Where(p => p.LineId == lineId);
        return await q.Select(p => new OasQrTokenDto { PostId = p.Id, Token = p.QrToken }).ToListAsync();
    }

    // ---- Completeness (spec §7.3, formula corrections v15) --------------------------
    public async Task<OasCompletenessDto> GetCompletenessAsync(int tenantId)
    {
        var totalPosts = await _db.Set<OasPost>().CountAsync();
        var postsWithCadence = totalPosts == 0 ? 0 : await _db.Database.SqlQueryRaw<int>(
            """select count(distinct post_id)::int as "Value" from oas_routings""").FirstOrDefaultAsync();

        var totalProducts = await _db.Database.SqlQueryRaw<int>("""select count(*)::int as "Value" from oas_products""").FirstOrDefaultAsync();
        var namedProducts = await _db.Database.SqlQueryRaw<int>("""select count(*)::int as "Value" from oas_products where name is not null and name <> ''""").FirstOrDefaultAsync();

        var totalCauses = await _db.Database.SqlQueryRaw<int>("""select count(*)::int as "Value" from oas_causes""").FirstOrDefaultAsync();
        // v15: this ratio must vary with real data, not a tautological
        // always-100% filter on a required column — count causes that are
        // actually reachable from a declared event_type as the "mapped" set.
        var mappedCauses = await _db.Database.SqlQueryRaw<int>("""select count(*)::int as "Value" from oas_causes where event_type is not null""").FirstOrDefaultAsync();

        var activeShifts = await _db.Database.SqlQueryRaw<int>("""select count(distinct code)::int as "Value" from oas_shift_templates where is_active""").FirstOrDefaultAsync();
        const int expectedShiftCount = 3; // morning/afternoon/night — configurable ambition, not a magic literal buried in a ratio (v15 fix)

        double Ratio(int num, int den) => den == 0 ? 0 : Math.Clamp((double)num / den, 0, 1);

        var postsRatio = Ratio(postsWithCadence, Math.Max(totalPosts, 1));
        var namedProductsRatio = Ratio(namedProducts, Math.Max(totalProducts, 1));
        var causesRatio = totalCauses == 0 ? 0 : Ratio(mappedCauses, totalCauses);
        var shiftRatio = Ratio(activeShifts, expectedShiftCount);

        var result = new OasCompletenessDto
        {
            PostsRatio = postsRatio,
            NamedProductsRatio = namedProductsRatio,
            ReferencesWithCadenceRatio = postsRatio,
            CausesRatio = causesRatio,
            ShiftCoverageRatio = shiftRatio,
        };
        result.Overall = new[] { result.PostsRatio, result.NamedProductsRatio, result.ReferencesWithCadenceRatio, result.CausesRatio, result.ShiftCoverageRatio }.Average();
        return result;
    }

    // ---- Layout -----------------------------------------------------------------
    public async Task<IReadOnlyList<OasPostLayoutDto>> GetLayoutAsync(int tenantId, Guid? lineId)
    {
        var postIds = _db.Set<OasPost>().AsQueryable();
        if (lineId is not null) postIds = postIds.Where(p => p.LineId == lineId);
        var ids = await postIds.Select(p => p.Id).ToListAsync();

        return await _db.Set<OasPostLayout>()
            .Where(l => ids.Contains(l.PostId))
            .OrderBy(l => l.SortOrder)
            .Select(l => new OasPostLayoutDto { PostId = l.PostId, LayoutKey = l.LayoutKey, SortOrder = l.SortOrder, ColSpan = l.ColSpan, RowSpan = l.RowSpan, X = l.X, Y = l.Y })
            .ToListAsync();
    }

    public async Task PutLayoutAsync(int tenantId, IReadOnlyList<OasPostLayoutDto> entries)
    {
        foreach (var entry in entries)
        {
            var existing = await _db.Set<OasPostLayout>().FirstOrDefaultAsync(l => l.PostId == entry.PostId && l.LayoutKey == entry.LayoutKey);
            if (existing is null)
            {
                _db.Set<OasPostLayout>().Add(new OasPostLayout
                {
                    TenantId = tenantId, PostId = entry.PostId, LayoutKey = entry.LayoutKey,
                    SortOrder = entry.SortOrder, ColSpan = entry.ColSpan, RowSpan = entry.RowSpan, X = entry.X, Y = entry.Y,
                });
            }
            else
            {
                existing.SortOrder = entry.SortOrder; existing.ColSpan = entry.ColSpan; existing.RowSpan = entry.RowSpan; existing.X = entry.X; existing.Y = entry.Y;
            }
        }
        await _db.SaveChangesAsync();
    }

    private static OasSiteDto ToSiteDto(OasSite s) => new() { Id = s.Id, Code = s.Code, Name = s.Name, Timezone = s.Timezone, Address = s.Address, Archived = s.ArchivedAt is not null };
    private static OasZoneDto ToZoneDto(OasZone z) => new() { Id = z.Id, SiteId = z.SiteId, Code = z.Code, Name = z.Name, SortOrder = z.SortOrder, Archived = z.ArchivedAt is not null };
    private static OasLineDto ToLineDto(OasLine l) => new() { Id = l.Id, ZoneId = l.ZoneId, Code = l.Code, Name = l.Name, SortOrder = l.SortOrder, TargetOee = l.TargetOee, Archived = l.ArchivedAt is not null };
    private static OasPostDto ToPostDto(OasPost p) => new() { Id = p.Id, LineId = p.LineId, Code = p.Code, Name = p.Name, SortOrder = p.SortOrder, PostType = p.PostType, IsCritical = p.IsCritical, IsActive = p.IsActive, Archived = p.ArchivedAt is not null };
}
