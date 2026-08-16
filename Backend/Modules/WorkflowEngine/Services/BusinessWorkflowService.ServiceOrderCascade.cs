// ============================================================================
// SERVICE ORDER CASCADE LOGIC - Updated Dispatch Rules (Partial class extension)
// 
// This file extends BusinessWorkflowService with cascade documentation.
// The actual cascade handler methods (HandleDispatchInProgressAsync, 
// HandleDispatchTechnicallyCompletedAsync, etc.) are defined in the main
// BusinessWorkflowService.cs file with full upward propagation logic.
//
// NEW DISPATCH STATUSES: pending, planned, confirmed, rejected, in_progress, completed
//
// CASCADE RULES:
// 1. If at least one dispatch is in_progress → Service Order = in_progress
// 2. If at least one dispatch is completed AND there are more than one dispatch → Service Order = partially_completed
// 3. If service order has ONE dispatch that is completed → Service Order = technically_completed
// 4. If dispatch is rejected → Service Order = planned
// ============================================================================

namespace MyApi.Modules.WorkflowEngine.Services
{
    // Partial class marker — all cascade methods live in BusinessWorkflowService.cs
    // This file is kept for documentation and future cascade extensions only.
    public partial class BusinessWorkflowService
    {
        // HandleDispatchInProgressAsync        → defined in BusinessWorkflowService.cs
        // HandleDispatchTechnicallyCompletedAsync → defined in BusinessWorkflowService.cs
        // HandleDispatchRejectedAsync           → defined in BusinessWorkflowService.cs (via HandleServiceOrderScheduledAsync flow)
    }
}
