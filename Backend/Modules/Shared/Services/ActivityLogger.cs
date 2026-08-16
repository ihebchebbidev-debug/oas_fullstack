using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyApi.Data;
using MyApi.Modules.Offers.Models;
using MyApi.Modules.Sales.Models;
using MyApi.Modules.Deals.Models;
using MyApi.Modules.Projects.Models;

namespace MyApi.Modules.Shared.Services
{
    /// <summary>
    /// Default <see cref="IActivityLogger"/> — writes both a SystemLog entry
    /// and a per-entity Activity row when applicable.
    ///
    /// A logging failure NEVER throws. Auditing is best-effort observability;
    /// if the audit write fails we swallow the exception so the caller's
    /// business transaction still commits (matches SystemLogService behavior).
    /// </summary>
    public class ActivityLogger : IActivityLogger
    {
        private readonly ISystemLogService _systemLog;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ActivityLogger> _logger;

        public ActivityLogger(
            ISystemLogService systemLog,
            ApplicationDbContext db,
            ILogger<ActivityLogger> logger)
        {
            _systemLog = systemLog;
            _db = db;
            _logger = logger;
        }

        public async Task LogAsync(ActivityLogEntry e)
        {
            // 1) Flat cross-module audit stream — always.
            try
            {
                await _systemLog.LogSuccessAsync(
                    message: e.Message,
                    module: e.Module,
                    action: e.Action,
                    userId: e.UserId,
                    userName: e.UserName,
                    entityType: e.EntityType,
                    entityId: e.EntityId,
                    details: e.Details);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ActivityLogger: SystemLog write failed for {Module}/{Action} {EntityType}#{EntityId}",
                    e.Module, e.Action, e.EntityType, e.EntityId);
            }

            // 2) Per-entity Activity table — parent hint OR top-level entity.
            try
            {
                await WriteEntityActivityAsync(e);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ActivityLogger: per-entity activity write failed for {Module}/{Action} {EntityType}#{EntityId}",
                    e.Module, e.Action, e.EntityType, e.EntityId);
            }
        }

        private async Task WriteEntityActivityAsync(ActivityLogEntry e)
        {
            // Resolve target: explicit parent hint wins; otherwise, if the entity
            // itself is a top-level parent, write to that parent's own table.
            string? parentType = e.ParentEntityType;
            int? parentId = e.ParentEntityId;

            if (parentType == null && (e.EntityType == "Offer" || e.EntityType == "Sale"
                || e.EntityType == "Deal" || e.EntityType == "Project"))
            {
                parentType = e.EntityType;
                if (int.TryParse(e.EntityId, out var pid)) parentId = pid;
            }

            if (parentType == null || !parentId.HasValue) return;

            var actor = e.UserName ?? e.UserId ?? "system";

            switch (parentType)
            {
                case "Offer":
                    _db.Set<OfferActivity>().Add(new OfferActivity
                    {
                        OfferId = parentId.Value,
                        Type = e.Action,
                        Description = e.Message,
                        CreatedByName = actor,
                        CreatedAt = DateTime.UtcNow,
                    });
                    break;

                case "Sale":
                    _db.Set<SaleActivity>().Add(new SaleActivity
                    {
                        SaleId = parentId.Value,
                        Type = e.Action,
                        Description = e.Message,
                        CreatedByName = actor,
                        CreatedAt = DateTime.UtcNow,
                    });
                    break;

                case "Deal":
                    _db.Set<DealActivity>().Add(new DealActivity
                    {
                        DealId = parentId.Value,
                        Type = e.Action,
                        Description = e.Message,
                        Details = e.Details,
                        OldValue = Truncate(e.OldValue, 100),
                        NewValue = Truncate(e.NewValue, 100),
                        CreatedBy = e.UserId ?? "system",
                        CreatedByName = e.UserName,
                        CreatedAt = DateTime.UtcNow,
                    });
                    break;

                case "Project":
                    _db.Set<ProjectActivity>().Add(new ProjectActivity
                    {
                        ProjectId = parentId.Value,
                        ActionType = e.Action,
                        Description = Truncate(e.Message, 500) ?? string.Empty,
                        Details = Truncate(e.Details, 1000),
                        CreatedBy = Truncate(actor, 255) ?? "system",
                        CreatedDate = DateTime.UtcNow,
                        RelatedEntityType = e.EntityType == parentType ? null : e.EntityType,
                        RelatedEntityId = e.EntityType == parentType ? null
                            : (int.TryParse(e.EntityId, out var cid) ? cid : (int?)null),
                    });
                    break;

                default:
                    return;
            }

            await _db.SaveChangesAsync();
        }

        private static string? Truncate(string? s, int max)
            => string.IsNullOrEmpty(s) ? s : (s!.Length <= max ? s : s.Substring(0, max));
    }
}
