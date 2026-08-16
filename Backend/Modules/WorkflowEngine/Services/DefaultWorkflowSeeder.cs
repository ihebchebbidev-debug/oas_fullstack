using MyApi.Data;
using MyApi.Modules.WorkflowEngine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MyApi.Modules.WorkflowEngine.Services
{
    /// <summary>
    /// Seeds the default business workflow when a fresh admin user is created.
    /// This ensures every new tenant starts with the standard pipeline:
    /// Offer → Sale → Service Order → Dispatch with proper status cascading.
    /// </summary>
    public interface IDefaultWorkflowSeeder
    {
        Task SeedDefaultWorkflowAsync(string createdBy);
    }

    public class DefaultWorkflowSeeder : IDefaultWorkflowSeeder
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<DefaultWorkflowSeeder> _logger;

        public DefaultWorkflowSeeder(ApplicationDbContext db, ILogger<DefaultWorkflowSeeder> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task SeedDefaultWorkflowAsync(string createdBy)
        {
            try
            {
                // Check if a default workflow already exists
                var existingWorkflow = await _db.WorkflowDefinitions
                    .AnyAsync(w => w.Name == "Default Business Workflow" && !w.IsDeleted);

                if (existingWorkflow)
                {
                    _logger.LogInformation("Default workflow already exists, skipping seed.");
                    return;
                }

                _logger.LogInformation("Seeding default business workflow for new admin: {CreatedBy}", createdBy);

                var nodes = GetDefaultNodes();
                var edges = GetDefaultEdges();

                var workflow = new WorkflowDefinition
                {
                    Name = "Default Business Workflow",
                    Description = "Automatic business pipeline: Offer → Sale → Service Order → Dispatch with status cascading rules.",
                    Nodes = JsonSerializer.Serialize(nodes),
                    Edges = JsonSerializer.Serialize(edges),
                    IsActive = true,
                    Version = 1,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow
                };

                _db.WorkflowDefinitions.Add(workflow);
                await _db.SaveChangesAsync();

                // Register all triggers
                var triggers = GetDefaultTriggers(workflow.Id);
                _db.WorkflowTriggers.AddRange(triggers);
                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "Default workflow seeded successfully. WorkflowId={WorkflowId}, Triggers={TriggerCount}",
                    workflow.Id, triggers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed default workflow");
                // Non-fatal: don't block signup if workflow seeding fails
            }
        }

        private static List<object> GetDefaultNodes()
        {
            return new List<object>
            {
                // 1. Offer Accepted → Create Sale
                new {
                    id = "trigger-offer-accepted",
                    type = "offer-status-trigger",
                    position = new { x = 100, y = 100 },
                    data = new {
                        label = "Offer Accepted",
                        type = "offer-status-trigger",
                        fromStatus = (string?)null,
                        toStatus = "accepted",
                        description = "Triggers when an offer is accepted"
                    }
                },
                new {
                    id = "action-create-sale",
                    type = "sale",
                    position = new { x = 400, y = 100 },
                    data = new {
                        label = "Create Sale",
                        type = "sale",
                        description = "Automatically create a sale from the accepted offer",
                        config = new { autoCreate = true, copyFromOffer = true }
                    }
                },

                // 2. Sale In Progress → Check for services → Create Service Order
                new {
                    id = "trigger-sale-in-progress",
                    type = "sale-status-trigger",
                    position = new { x = 100, y = 250 },
                    data = new {
                        label = "Sale In Progress",
                        type = "sale-status-trigger",
                        fromStatus = "created",
                        toStatus = "in_progress",
                        description = "Triggers when a sale starts processing"
                    }
                },
                new {
                    id = "condition-has-services",
                    type = "if-else",
                    position = new { x = 400, y = 250 },
                    data = new {
                        label = "Has Service Items?",
                        type = "if-else",
                        description = "Check if the sale contains at least one service item",
                        config = new {
                            conditionData = new {
                                field = "sale.items",
                                value = "service",
                                @operator = "contains",
                                checkField = "type"
                            }
                        }
                    }
                },
                new {
                    id = "action-create-service-order",
                    type = "service-order",
                    position = new { x = 700, y = 200 },
                    data = new {
                        label = "Create Service Order",
                        type = "service-order",
                        description = "Create a service order for the service items",
                        config = new { autoCreate = true, initialStatus = "pending", copyServicesFromSale = true }
                    }
                },

                // 3. Service Order Planned → Create Dispatches
                new {
                    id = "trigger-service-order-scheduled",
                    type = "service-order-status-trigger",
                    position = new { x = 100, y = 400 },
                    data = new {
                        label = "Service Order Planned",
                        type = "service-order-status-trigger",
                        fromStatus = "ready_for_planning",
                        toStatus = "planned",
                        description = "Triggers when a service order is planned"
                    }
                },
                new {
                    id = "action-create-dispatches",
                    type = "dispatch",
                    position = new { x = 400, y = 400 },
                    data = new {
                        label = "Create Dispatches",
                        type = "dispatch",
                        description = "Create dispatch entries (status: pending)",
                        config = new { autoCreate = true, initialStatus = "pending", createPerService = true }
                    }
                },

                // 4. Dispatch In Progress → SO In Progress
                new {
                    id = "trigger-dispatch-in-progress",
                    type = "dispatch-status-trigger",
                    position = new { x = 100, y = 550 },
                    data = new {
                        label = "Dispatch In Progress",
                        type = "dispatch-status-trigger",
                        fromStatus = "confirmed",
                        toStatus = "in_progress",
                        description = "Triggers when a dispatch starts"
                    }
                },
                new {
                    id = "action-service-order-in-progress",
                    type = "update-service-order-status",
                    position = new { x = 400, y = 550 },
                    data = new {
                        label = "Service Order → In Progress",
                        type = "update-service-order-status",
                        description = "Rule 1: At least one dispatch in_progress",
                        config = new { condition = "ifNotAlreadyInProgress", newStatus = "in_progress" }
                    }
                },

                // 5. Dispatch Rejected → SO back to planning
                new {
                    id = "trigger-dispatch-rejected",
                    type = "dispatch-status-trigger",
                    position = new { x = 100, y = 650 },
                    data = new {
                        label = "Dispatch Rejected",
                        type = "dispatch-status-trigger",
                        fromStatus = "assigned",
                        toStatus = "rejected",
                        description = "Triggers when a dispatch is rejected"
                    }
                },
                new {
                    id = "action-so-back-to-planning",
                    type = "update-service-order-status",
                    position = new { x = 400, y = 650 },
                    data = new {
                        label = "Service Order → Ready for Planning",
                        type = "update-service-order-status",
                        description = "Rule 4: Dispatch rejected → back to planning",
                        config = new { newStatus = "ready_for_planning" }
                    }
                },

                // 6. Dispatch Completed → Check all done → SO status
                new {
                    id = "trigger-dispatch-completed",
                    type = "dispatch-status-trigger",
                    position = new { x = 100, y = 800 },
                    data = new {
                        label = "Dispatch Completed",
                        type = "dispatch-status-trigger",
                        fromStatus = "in_progress",
                        toStatus = "completed",
                        description = "Triggers when a dispatch is completed"
                    }
                },
                new {
                    id = "condition-all-done",
                    type = "if-else",
                    position = new { x = 400, y = 800 },
                    data = new {
                        label = "All Dispatches Completed?",
                        type = "if-else",
                        description = "Check if all dispatches are completed",
                        config = new {
                            conditionData = new {
                                field = "serviceOrder.dispatches",
                                value = "completed",
                                @operator = "all_match",
                                checkField = "status"
                            }
                        }
                    }
                },
                new {
                    id = "action-so-tech-complete",
                    type = "update-service-order-status",
                    position = new { x = 700, y = 750 },
                    data = new {
                        label = "Service Order → Technically Completed",
                        type = "update-service-order-status",
                        description = "Rule 3: All/single dispatch completed",
                        config = new { newStatus = "technically_completed" }
                    }
                },
                new {
                    id = "action-so-partial",
                    type = "update-service-order-status",
                    position = new { x = 700, y = 880 },
                    data = new {
                        label = "Service Order → Partially Completed",
                        type = "update-service-order-status",
                        description = "Rule 2: Some dispatches completed",
                        config = new { newStatus = "partially_completed" }
                    }
                },

                // 7. Sale Closed → Service Order Invoiced
                new {
                    id = "trigger-sale-closed",
                    type = "sale-status-trigger",
                    position = new { x = 100, y = 950 },
                    data = new {
                        label = "Sale Closed",
                        type = "sale-status-trigger",
                        fromStatus = (string?)null,
                        toStatus = "closed",
                        description = "Triggers when a sale is closed"
                    }
                },
                new {
                    id = "action-so-invoiced",
                    type = "update-service-order-status",
                    position = new { x = 400, y = 950 },
                    data = new {
                        label = "Service Order → Invoiced",
                        type = "update-service-order-status",
                        description = "When sale is closed, mark the linked service order as invoiced",
                        config = new { newStatus = "invoiced" }
                    }
                }
            };
        }

        private static List<object> GetDefaultEdges()
        {
            return new List<object>
            {
                new { id = "edge-1", source = "trigger-offer-accepted", target = "action-create-sale", type = "smoothstep" },
                new { id = "edge-2", source = "trigger-sale-in-progress", target = "condition-has-services", type = "smoothstep" },
                new { id = "edge-3", source = "condition-has-services", target = "action-create-service-order", sourceHandle = "yes", type = "smoothstep", label = "YES" },
                new { id = "edge-4", source = "trigger-service-order-scheduled", target = "action-create-dispatches", type = "smoothstep" },
                new { id = "edge-5", source = "trigger-dispatch-in-progress", target = "action-service-order-in-progress", type = "smoothstep" },
                new { id = "edge-6", source = "trigger-dispatch-rejected", target = "action-so-back-to-planning", type = "smoothstep" },
                new { id = "edge-7", source = "trigger-dispatch-completed", target = "condition-all-done", type = "smoothstep" },
                new { id = "edge-8", source = "condition-all-done", target = "action-so-tech-complete", sourceHandle = "yes", type = "smoothstep", label = "YES" },
                new { id = "edge-9", source = "condition-all-done", target = "action-so-partial", sourceHandle = "no", type = "smoothstep", label = "NO" },
                new { id = "edge-10", source = "trigger-sale-closed", target = "action-so-invoiced", type = "smoothstep" }
            };
        }

        private static List<WorkflowTrigger> GetDefaultTriggers(int workflowId)
        {
            return new List<WorkflowTrigger>
            {
                new WorkflowTrigger
                {
                    WorkflowId = workflowId,
                    NodeId = "trigger-offer-accepted",
                    EntityType = "offer",
                    FromStatus = null,
                    ToStatus = "accepted",
                    IsActive = true
                },
                new WorkflowTrigger
                {
                    WorkflowId = workflowId,
                    NodeId = "trigger-sale-in-progress",
                    EntityType = "sale",
                    FromStatus = "created",
                    ToStatus = "in_progress",
                    IsActive = true
                },
                new WorkflowTrigger
                {
                    WorkflowId = workflowId,
                    NodeId = "trigger-service-order-scheduled",
                    EntityType = "service_order",
                    FromStatus = "ready_for_planning",
                    ToStatus = "planned",
                    IsActive = true
                },
                new WorkflowTrigger
                {
                    WorkflowId = workflowId,
                    NodeId = "trigger-dispatch-in-progress",
                    EntityType = "dispatch",
                    FromStatus = "confirmed",
                    ToStatus = "in_progress",
                    IsActive = true
                },
                new WorkflowTrigger
                {
                    WorkflowId = workflowId,
                    NodeId = "trigger-dispatch-rejected",
                    EntityType = "dispatch",
                    FromStatus = "assigned",
                    ToStatus = "rejected",
                    IsActive = true
                },
                new WorkflowTrigger
                {
                    WorkflowId = workflowId,
                    NodeId = "trigger-dispatch-completed",
                    EntityType = "dispatch",
                    FromStatus = "in_progress",
                    ToStatus = "completed",
                    IsActive = true
                },
                new WorkflowTrigger
                {
                    WorkflowId = workflowId,
                    NodeId = "trigger-sale-closed",
                    EntityType = "sale",
                    FromStatus = null,
                    ToStatus = "closed",
                    IsActive = true
                }
            };
        }
    }
}
