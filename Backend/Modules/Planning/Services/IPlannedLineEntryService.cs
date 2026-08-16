using System.Collections.Generic;
using System.Threading.Tasks;
using MyApi.Modules.Planning.DTOs;

namespace MyApi.Modules.Planning.Services
{
    public interface IPlannedLineEntryService
    {
        Task<List<PlannedLineEntryDto>> GetForParentAsync(string parentType, int parentId);
        Task<PlannedLineEntryDto> CreateAsync(string parentType, int parentId, CreatePlannedLineEntryDto dto, string userId);
        Task<PlannedLineEntryDto> UpdateAsync(int id, UpdatePlannedLineEntryDto dto, string userId);
        /// <summary>Delete a planned entry. Returns a non-null sync warning message when the primary delete succeeded but the propagation to linked offer/sale/job planned entries failed.</summary>
        Task<string?> DeleteAsync(int id, string userId);

        /// <summary>Copy all planned entries from one parent (e.g. offer_item) to another (sale_item / service_order_job), preserving lineage.</summary>
        Task CopyAsync(string sourceParentType, int sourceParentId, string targetParentType, int targetParentId, string userId);

        /// <summary>Roll up planned vs actual for a service order job (sums TimeEntries + Expenses already logged).</summary>
        Task<PlanVsActualLineDto> GetPlanVsActualAsync(int serviceOrderJobId);
    }
}
