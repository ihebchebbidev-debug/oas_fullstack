using System;
using System.Collections.Generic;
using System.Linq;

namespace MyApi.Modules.ServiceOrders.Services
{
    /// <summary>
    /// Outcome of recomputing a service order's status from its dispatches.
    /// </summary>
    public sealed class ServiceOrderStatusResult
    {
        /// <summary>The status the service order should have.</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>Number of live (non-cancelled, non-rejected) dispatches that are done.</summary>
        public int CompletedDispatchCount { get; init; }

        /// <summary>Number of live (non-cancelled, non-rejected) dispatches.</summary>
        public int ActiveDispatchCount { get; init; }

        /// <summary>Number of non-deleted dispatches, including cancelled/rejected ones.</summary>
        public int TotalDispatchCount { get; init; }

        /// <summary>True when <see cref="Status"/> differs from the status passed in.</summary>
        public bool StatusChanged { get; init; }

        /// <summary>True when the service order is in a status whose value must never be recomputed.</summary>
        public bool IsTerminal { get; init; }
    }

    /// <summary>
    /// THE single source of truth for "what status should this service order have, given its
    /// dispatches?".
    ///
    /// This class exists because the same question used to be answered independently in three
    /// places (BusinessWorkflowService.HandleDispatchTechnicallyCompletedAsync,
    /// DispatchService.RecalculateServiceOrderStatusAsync and
    /// ServiceOrderService.RecalculateStatusFromDispatchesAsync), and the three answers did not
    /// agree — most visibly for "no live dispatches left" (partially_completed vs
    /// ready_for_planning vs cancelled) and for the completion state itself
    /// (ready_for_invoice vs technically_completed). Whichever code path happened to run last
    /// won, so identical dispatch sets produced different service order statuses.
    ///
    /// Business rules encoded here (deliberately, in one place):
    ///
    /// 1. Completion stops at <c>technically_completed</c>. Finishing the field work never
    ///    auto-advances an order to <c>ready_for_invoice</c>: a human reviews the consumed
    ///    materials/time/expenses and explicitly moves the order on. Multiple dispatches per
    ///    service order are fully supported — the order only reaches
    ///    <c>technically_completed</c> once *every* live dispatch is done.
    /// 2. Cancelled and rejected dispatches are excluded from the "is everything done?"
    ///    denominator, so an abandoned attempt can be re-dispatched without permanently
    ///    blocking completion.
    /// 3. A service order in a hard-terminal status (<c>invoiced</c>, <c>closed</c>,
    ///    <c>cancelled</c>) is never restatused by dispatch activity; only its counter refreshes.
    /// 4. A service order already past review (<c>ready_for_invoice</c>, <c>completed</c>) is
    ///    never walked backwards to <c>technically_completed</c>, but genuinely new unfinished
    ///    work does reopen it — so re-planning an order any number of times keeps working.
    /// 5. <c>on_hold</c> is a human decision and survives dispatch churn until the work is
    ///    actually finished.
    /// </summary>
    public static class ServiceOrderStatusCalculator
    {
        /// <summary>Dispatch statuses excluded from the completion denominator.</summary>
        private static readonly HashSet<string> DeadDispatchStatuses =
            new(StringComparer.OrdinalIgnoreCase) { "cancelled", "rejected" };

        /// <summary>Dispatch statuses that count as "field work done".</summary>
        private static readonly HashSet<string> CompletedDispatchStatuses =
            new(StringComparer.OrdinalIgnoreCase) { "completed", "technically_completed" };

        /// <summary>Service order statuses whose value is never recomputed from dispatches.</summary>
        private static readonly HashSet<string> HardTerminalStatuses =
            new(StringComparer.OrdinalIgnoreCase) { "invoiced", "closed", "cancelled" };

        /// <summary>
        /// Statuses a human moved the order into after the field work finished. Dispatch
        /// recalculation may not downgrade these to <c>technically_completed</c>.
        /// </summary>
        private static readonly HashSet<string> PostReviewStatuses =
            new(StringComparer.OrdinalIgnoreCase) { "ready_for_invoice", "completed" };

        /// <summary>The status a service order reaches when every live dispatch is done.</summary>
        public const string FieldWorkCompleteStatus = "technically_completed";

        /// <summary>True when the dispatch status counts as "field work done".</summary>
        public static bool IsCompletedDispatchStatus(string? status) =>
            CompletedDispatchStatuses.Contains((status ?? string.Empty).Trim());

        /// <summary>True when the dispatch status is cancelled/rejected.</summary>
        public static bool IsDeadDispatchStatus(string? status) =>
            DeadDispatchStatuses.Contains((status ?? string.Empty).Trim());

        /// <summary>True when the service order status must never be recomputed from dispatches.</summary>
        public static bool IsHardTerminalStatus(string? status) =>
            HardTerminalStatuses.Contains((status ?? string.Empty).Trim());

        /// <summary>
        /// Recompute the status a service order should have.
        /// </summary>
        /// <param name="currentStatus">The service order's current status.</param>
        /// <param name="dispatchStatuses">
        /// Statuses of every <b>non-deleted</b> dispatch linked to the service order. The caller
        /// is responsible for filtering out soft-deleted dispatches.
        /// </param>
        public static ServiceOrderStatusResult Compute(string? currentStatus, IEnumerable<string?> dispatchStatuses)
        {
            var statuses = (dispatchStatuses ?? Enumerable.Empty<string?>())
                .Select(s => (s ?? string.Empty).Trim())
                .ToList();

            var active = statuses.Where(s => !DeadDispatchStatuses.Contains(s)).ToList();
            var completedCount = active.Count(s => CompletedDispatchStatuses.Contains(s));

            var current = (currentStatus ?? string.Empty).Trim();

            // Rule 3: hard-terminal orders keep their status; only the counter is refreshed.
            if (HardTerminalStatuses.Contains(current))
            {
                return new ServiceOrderStatusResult
                {
                    Status = current,
                    CompletedDispatchCount = completedCount,
                    ActiveDispatchCount = active.Count,
                    TotalDispatchCount = statuses.Count,
                    StatusChanged = false,
                    IsTerminal = true
                };
            }

            string candidate;

            if (statuses.Count == 0)
            {
                // Nothing has ever been planned (or every dispatch was deleted) → plan it.
                candidate = "ready_for_planning";
            }
            else if (active.Count == 0)
            {
                // Dispatches exist but every one of them was cancelled/rejected. Cascade the
                // cancellation up rather than reporting the order as partially complete.
                candidate = "cancelled";
            }
            else if (completedCount == active.Count)
            {
                // Rule 1: every live dispatch is done. Stop here — a human advances the order
                // to ready_for_invoice after reviewing what was consumed.
                candidate = FieldWorkCompleteStatus;
            }
            else if (active.Any(s => string.Equals(s, "in_progress", StringComparison.OrdinalIgnoreCase)))
            {
                candidate = "in_progress";
            }
            else if (completedCount > 0)
            {
                // Some dispatches finished, others are still planned/assigned.
                candidate = "partially_completed";
            }
            else
            {
                // Work is dispatched but nothing started yet. 'planned' is the single
                // pre-execution status (the duplicate 'scheduled' status was removed).
                candidate = "planned";
            }

            // Rule 4: never downgrade an order a human already pushed past review. New,
            // unfinished work still reopens it (candidate would be planned/in_progress).
            if (PostReviewStatuses.Contains(current) && candidate == FieldWorkCompleteStatus)
            {
                candidate = current;
            }

            // Rule 5: on_hold is a human decision; keep it until the work is genuinely finished.
            if (string.Equals(current, "on_hold", StringComparison.OrdinalIgnoreCase)
                && candidate != FieldWorkCompleteStatus
                && candidate != "cancelled")
            {
                candidate = current;
            }

            return new ServiceOrderStatusResult
            {
                Status = candidate,
                CompletedDispatchCount = completedCount,
                ActiveDispatchCount = active.Count,
                TotalDispatchCount = statuses.Count,
                StatusChanged = !string.Equals(candidate, current, StringComparison.Ordinal),
                IsTerminal = false
            };
        }
    }
}
