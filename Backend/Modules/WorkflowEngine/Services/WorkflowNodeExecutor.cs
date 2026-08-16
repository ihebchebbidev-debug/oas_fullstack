using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.WorkflowEngine.Models;
using MyApi.Modules.Offers.Models;
using MyApi.Modules.Sales.Models;
using MyApi.Modules.ServiceOrders.Models;
using MyApi.Modules.Dispatches.Models;
using MyApi.Modules.Notifications.DTOs;
using MyApi.Modules.Notifications.Services;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Text.Json;
// Jint 4.x exposes Options config (LimitRecursion/MaxStatements/TimeoutInterval/
// CancellationToken) and JsValue helpers (IsUndefined/IsNull) as extension methods
// in the Jint namespace, so this using is required for the JS sandbox below.
using Jint;
using MyApi.Modules.Deals.Services;
using MyApi.Modules.Deals.DTOs;

namespace MyApi.Modules.WorkflowEngine.Services
{
    public class WorkflowNodeExecutor : IWorkflowNodeExecutor
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<WorkflowNodeExecutor> _logger;
        private readonly IBusinessWorkflowService _businessWorkflowService;
        private readonly IConfiguration _configuration;
        private readonly INotificationService _notificationService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceProvider _serviceProvider;

        public WorkflowNodeExecutor(
            ApplicationDbContext db,
            ILogger<WorkflowNodeExecutor> logger,
            IBusinessWorkflowService businessWorkflowService,
            IConfiguration configuration,
            INotificationService notificationService,
            IHttpClientFactory httpClientFactory,
            IServiceProvider serviceProvider)
        {
            _db = db;
            _logger = logger;
            _businessWorkflowService = businessWorkflowService;
            _configuration = configuration;
            _notificationService = notificationService;
            _httpClientFactory = httpClientFactory;
            _serviceProvider = serviceProvider;
        }

        public async Task<NodeExecutionResult> ExecuteNodeAsync(
            int executionId,
            WorkflowNode node,
            WorkflowExecutionContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new NodeExecutionResult();

            try
            {
                _logger.LogInformation(
                    "Executing node {NodeId} of type {NodeType} for execution {ExecutionId}",
                    node.Id, node.Type, executionId);

                // Route to appropriate handler based on node type
                // Get the actionType from node data for entity nodes
                var actionType = GetNodeDataString(node, "actionType")?.ToLower() ?? "";
                
                result = node.Type.ToLower() switch
                {
                    // Trigger nodes (already fired, just pass through)
                    var t when t.Contains("status-trigger") => await ExecuteTriggerNodeAsync(node, context),
                    var t when t.Contains("trigger") => await ExecuteTriggerNodeAsync(node, context),

                    // P0 FIX: these node types were previously falling through to the
                    // wrong handler (wait-for-event → ExecuteDelayAsync because of the
                    // "wait" substring match below; human-input-form / create-deal /
                    // update-deal-status → ExecuteDefaultNodeAsync which no-ops).
                    // Until dedicated executors ship we mark them as failed with a
                    // clear message so users see the gap in the execution log instead
                    // of a silently succeeded "delay" or empty step. See workflow module
                    // plan Phase 1 item 3.
                    "wait-for-event" or "wait_for_event" => new NodeExecutionResult
                    {
                        Success = false, Status = "failed",
                        Error = "wait-for-event executor is not yet implemented — remove this node or use a delay/approval node instead."
                    },
                    "human-input-form" or "human_input_form" => new NodeExecutionResult
                    {
                        Success = false, Status = "failed",
                        Error = "human-input-form executor is not yet implemented — use an approval node for now."
                    },
                    "create-deal" or "create_deal" => await ExecuteCreateDealAsync(node, context),
                    "update-deal-status" or "update_deal_status"
                        or "update-deal-stage" or "update_deal_stage" => await ExecuteUpdateDealStageAsync(node, context),
                    
                    // Business process nodes (explicit create- prefixed)
                    var t when t.Contains("create-offer") => await ExecuteCreateOfferAsync(node, context),
                    var t when t.Contains("create-sale") => await ExecuteCreateSaleAsync(node, context),
                    var t when t.Contains("create-service") => await ExecuteCreateServiceOrderAsync(node, context),
                    var t when t.Contains("create-dispatch") => await ExecuteCreateDispatchAsync(node, context),
                    
                    // Status update nodes (explicit update- prefixed)
                    var t when t.Contains("update-status") || t.Contains("update-") => await ExecuteUpdateStatusAsync(node, context),
                    
                    // Plain entity names used by frontend action nodes
                    // These are nodes with type "sale", "service-order", "dispatch", "offer"
                    // that have actionType "create" or "update-status" in their data
                    "offer" when actionType == "create" => await ExecuteCreateOfferAsync(node, context),
                    "sale" when actionType == "create" => await ExecuteCreateSaleAsync(node, context),
                    "service-order" when actionType == "create" => await ExecuteCreateServiceOrderAsync(node, context),
                    "service_order" when actionType == "create" => await ExecuteCreateServiceOrderAsync(node, context),
                    "dispatch" when actionType == "create" => await ExecuteCreateDispatchAsync(node, context),
                    // Plain entity names with update-status action
                    "offer" when actionType.Contains("update") => await ExecuteUpdateStatusAsync(node, context),
                    "sale" when actionType.Contains("update") => await ExecuteUpdateStatusAsync(node, context),
                    "service-order" when actionType.Contains("update") => await ExecuteUpdateStatusAsync(node, context),
                    "service_order" when actionType.Contains("update") => await ExecuteUpdateStatusAsync(node, context),
                    "dispatch" when actionType.Contains("update") => await ExecuteUpdateStatusAsync(node, context),
                    // Plain entity names without actionType - default to create
                    "offer" => await ExecuteCreateOfferAsync(node, context),
                    "sale" => await ExecuteCreateSaleAsync(node, context),
                    "service-order" or "service_order" => await ExecuteCreateServiceOrderAsync(node, context),
                    "dispatch" => await ExecuteCreateDispatchAsync(node, context),
                    
                    // Contact node - lookup/pass through contact info
                    "contact" => await ExecuteContactNodeAsync(node, context),
                    
                    // Condition nodes
                    var t when t.Contains("condition") || t.Contains("if-") => await ExecuteConditionNodeAsync(node, context),
                    var t when t.Contains("switch") => await ExecuteSwitchNodeAsync(node, context),
                    
                    // Action nodes
                    var t when t.Contains("send-email") || t.Contains("email") => await ExecuteSendEmailAsync(node, context),
                    var t when t.Contains("send-sms") || t.Contains("sms") => await ExecuteSendSmsAsync(node, context),
                    var t when t.Contains("notification") => await ExecuteNotificationAsync(node, context),
                    
                    // Integration nodes
                    var t when t.Contains("api") || t.Contains("http") => await ExecuteApiCallAsync(node, context),
                    var t when t.Contains("webhook") => await ExecuteWebhookAsync(node, context),
                    
                    // Approval nodes
                    var t when t.Contains("approval") => await ExecuteApprovalAsync(node, context, executionId),
                    
                    // Delay nodes
                    var t when t.Contains("delay") || t.Contains("wait") => await ExecuteDelayAsync(node, context),
                    
                    // AI nodes
                    var t when t.Contains("ai-") || t.Contains("llm") => await ExecuteAiNodeAsync(node, context),
                    
                    // Loop nodes
                    var t when t.Contains("loop") => await ExecuteLoopNodeAsync(node, context),
                    
                    // Calculation nodes
                    var t when t.Contains("calculate") || t.Contains("math") => await ExecuteCalculationAsync(node, context),
                    var t when t.Contains("set-variable") || t.Contains("assign") => await ExecuteSetVariableAsync(node, context),
                    
                    // Database nodes
                    var t when t.Contains("database") || t.Contains("db-") => await ExecuteDatabaseOperationAsync(node, context),
                    
                    // Parallel node
                    var t when t.Contains("parallel") => await ExecuteParallelNodeAsync(node, context),
                    
                    // Try-catch node
                    var t when t.Contains("try-catch") || t.Contains("error") => await ExecuteTryCatchNodeAsync(node, context),
                    
                    // Scheduled trigger (already fired, pass through like other triggers)
                    var t when t.Contains("scheduled") => await ExecuteTriggerNodeAsync(node, context),
                    
                    // Dynamic Form nodes
                    var t when t.Contains("dynamic-form") || t.Contains("dynamic_form") => await ExecuteDynamicFormNodeAsync(node, context),
                    
                    // Data Transfer nodes
                    var t when t.Contains("data-transfer") || t.Contains("data_transfer") => await ExecuteDataTransferNodeAsync(node, context),
                    
                    // Custom LLM nodes
                    var t when t.Contains("custom-llm") || t.Contains("custom_llm") => await ExecuteCustomLLMNodeAsync(node, context),
                    
                    // Subworkflow nodes
                    var t when t.Contains("subworkflow") || t.Contains("sub-workflow") || t.Contains("sub_workflow") || t == "call-workflow" => await ExecuteSubWorkflowAsync(node, context),

                    // Code / JavaScript nodes
                    var t when t == "code" || t == "javascript" || t.Contains("code") => await ExecuteCodeNodeAsync(node, context),
                    
                    // Default - just pass through
                    _ => await ExecuteDefaultNodeAsync(node, context)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing node {NodeId} of type {NodeType}", node.Id, node.Type);
                result.Success = false;
                result.Status = "failed";
                result.Error = ex.Message;
            }

            stopwatch.Stop();
            result.DurationMs = (int)stopwatch.ElapsedMilliseconds;

            return result;
        }

        public async Task<bool> EvaluateConditionAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var result = await ExecuteConditionNodeAsync(node, context);
            return result.SelectedBranch == "true" || result.SelectedBranch == "yes";
        }

        #region Trigger Nodes

        private Task<NodeExecutionResult> ExecuteTriggerNodeAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            // Trigger nodes have already fired - they just pass data through
            return Task.FromResult(new NodeExecutionResult
            {
                Success = true,
                Status = "completed",
                Output = new Dictionary<string, object?>
                {
                    ["triggered"] = true,
                    ["entityType"] = context.TriggerEntityType,
                    ["entityId"] = context.TriggerEntityId
                }
            });
        }

        #endregion

        #region Business Process Nodes

        private Task<NodeExecutionResult> ExecuteCreateOfferAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            // RELIABILITY FIX (BUG-2): previously this stub returned Success=true with
            // status="simulated", so workflows containing a Create-Offer node looked
            // healthy while the offer was never created. Fail loudly so users notice
            // the limitation instead of trusting a silent no-op.
            const string message =
                "create-offer node is not implemented yet — workflow halted to avoid a silent no-op. " +
                "Remove the node, or create the offer manually via the Offers module.";
            _logger.LogError("[WORKFLOW] {Message} (execution context: {EntityType}:{EntityId})",
                message, context.TriggerEntityType, context.TriggerEntityId);
            return Task.FromResult(new NodeExecutionResult
            {
                Success = false,
                Status = "failed",
                Error = message,
                ShouldStop = true
            });
        }

        private async Task<NodeExecutionResult> ExecuteCreateSaleAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            // If triggered by an offer, create sale from offer
            if (context.TriggerEntityType == "offer")
            {
                var saleId = await _businessWorkflowService.HandleOfferAcceptedAsync(
                    context.TriggerEntityId, 
                    context.UserId);

                if (saleId.HasValue)
                {
                    return new NodeExecutionResult
                    {
                        Success = true,
                        Status = "completed",
                        CreatedEntityId = saleId.Value,
                        CreatedEntityType = "sale",
                        Output = new Dictionary<string, object?>
                        {
                            ["action"] = "create_sale",
                            ["saleId"] = saleId.Value,
                            ["fromOfferId"] = context.TriggerEntityId
                        }
                    };
                }
            }

            return new NodeExecutionResult
            {
                Success = true,
                Status = "completed",
                Output = new Dictionary<string, object?>
                {
                    ["action"] = "create_sale",
                    ["status"] = "skipped",
                    ["reason"] = "No offer context or sale already exists"
                }
            };
        }

        private async Task<NodeExecutionResult> ExecuteCreateServiceOrderAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            // If triggered by a sale, create service order
            if (context.TriggerEntityType == "sale")
            {
                // Extract service order config from context if available
                object? serviceOrderConfig = null;
                if (context.Variables?.TryGetValue("additionalContext", out var additionalContext) == true && additionalContext != null)
                {
                    try
                    {
                        // The additionalContext may contain serviceOrderConfig
                        var contextJson = System.Text.Json.JsonSerializer.Serialize(additionalContext);
                        var contextDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(contextJson);
                        
                        if (contextDict != null && 
                            (contextDict.TryGetValue("serviceOrderConfig", out var configVal) || 
                             contextDict.TryGetValue("ServiceOrderConfig", out configVal)))
                        {
                            serviceOrderConfig = configVal;
                            _logger.LogInformation("ExecuteCreateServiceOrderAsync: Found serviceOrderConfig in context");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "ExecuteCreateServiceOrderAsync: Failed to parse additionalContext");
                    }
                }
                
                var serviceOrderId = await _businessWorkflowService.HandleSaleInProgressAsync(
                    context.TriggerEntityId, 
                    context.UserId,
                    serviceOrderConfig);

                if (serviceOrderId.HasValue)
                {
                    return new NodeExecutionResult
                    {
                        Success = true,
                        Status = "completed",
                        CreatedEntityId = serviceOrderId.Value,
                        CreatedEntityType = "service_order",
                        Output = new Dictionary<string, object?>
                        {
                            ["action"] = "create_service_order",
                            ["serviceOrderId"] = serviceOrderId.Value,
                            ["fromSaleId"] = context.TriggerEntityId,
                            ["usedConfig"] = serviceOrderConfig != null
                        }
                    };
                }
            }

            return new NodeExecutionResult
            {
                Success = true,
                Status = "completed",
                Output = new Dictionary<string, object?>
                {
                    ["action"] = "create_service_order",
                    ["status"] = "skipped",
                    ["reason"] = "No sale context or no service items"
                }
            };
        }

        private async Task<NodeExecutionResult> ExecuteCreateDispatchAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            // If triggered by a service order, create dispatches
            if (context.TriggerEntityType == "service_order")
            {
                var dispatchIds = await _businessWorkflowService.HandleServiceOrderScheduledAsync(
                    context.TriggerEntityId, 
                    context.UserId);

                var ids = dispatchIds.ToList();
                if (ids.Any())
                {
                    return new NodeExecutionResult
                    {
                        Success = true,
                        Status = "completed",
                        CreatedEntityId = ids.First(),
                        CreatedEntityType = "dispatch",
                        Output = new Dictionary<string, object?>
                        {
                            ["action"] = "create_dispatch",
                            ["dispatchIds"] = ids,
                            ["count"] = ids.Count,
                            ["fromServiceOrderId"] = context.TriggerEntityId
                        }
                    };
                }
            }

            return new NodeExecutionResult
            {
                Success = true,
                Status = "completed",
                Output = new Dictionary<string, object?>
                {
                    ["action"] = "create_dispatch",
                    ["status"] = "skipped",
                    ["reason"] = "No service order context or dispatches already exist"
                }
            };
        }

        // =====================================================================
        // Deal executors (wired via IServiceProvider so the workflow module
        // stays loosely coupled to Deals). Reads node.data fields with the
        // usual variable resolver so users can set title/stage/etc. from
        // upstream node outputs — e.g. Title = "{{trigger.subject}}".
        // =====================================================================

        private async Task<NodeExecutionResult> ExecuteCreateDealAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var dealService = _serviceProvider.GetService(typeof(IDealService)) as IDealService;
            if (dealService == null)
            {
                return new NodeExecutionResult
                {
                    Success = false, Status = "failed",
                    Error = "IDealService is not registered — the Deals module must be enabled to use create-deal nodes."
                };
            }

            var title = ResolveVariables(GetNodeDataString(node, "title") ?? GetNodeDataString(node, "dealTitle") ?? "", context).Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return new NodeExecutionResult
                {
                    Success = false, Status = "failed",
                    Error = "create-deal: 'title' is required. Set node.data.title (variables like {{trigger.subject}} are supported)."
                };
            }

            // Contact ID: try node config, then trigger context (if a contact triggered the flow),
            // then created_contact_id if a previous node created one.
            int contactId = 0;
            var contactIdRaw = ResolveVariables(GetNodeDataString(node, "contactId") ?? "", context).Trim();
            if (int.TryParse(contactIdRaw, out var parsedCid) && parsedCid > 0)
                contactId = parsedCid;
            else if (context.TriggerEntityType == "contact")
                contactId = context.TriggerEntityId;
            else if (context.Variables.TryGetValue("created_contact_id", out var cc) && cc != null
                     && int.TryParse(cc.ToString(), out var cid2))
                contactId = cid2;

            if (contactId <= 0)
            {
                return new NodeExecutionResult
                {
                    Success = false, Status = "failed",
                    Error = "create-deal: could not determine contactId. Set node.data.contactId or trigger the workflow from a contact."
                };
            }

            var dto = new CreateDealDto
            {
                Title = title,
                Description = ResolveVariables(GetNodeDataString(node, "description") ?? "", context),
                ContactId = contactId,
                Stage = ResolveVariables(GetNodeDataString(node, "stage") ?? "lead", context),
                Category = ResolveVariables(GetNodeDataString(node, "category") ?? "", context) is var cat && string.IsNullOrWhiteSpace(cat) ? null : cat,
                Source = ResolveVariables(GetNodeDataString(node, "source") ?? "workflow", context),
                AssignedTo = ResolveVariables(GetNodeDataString(node, "assignedTo") ?? "", context) is var at && string.IsNullOrWhiteSpace(at) ? null : at,
                Notes = ResolveVariables(GetNodeDataString(node, "notes") ?? "", context),
            };

            var probRaw = ResolveVariables(GetNodeDataString(node, "probability") ?? "", context);
            if (int.TryParse(probRaw, out var prob)) dto.Probability = Math.Clamp(prob, 0, 100);

            var valRaw = ResolveVariables(GetNodeDataString(node, "estimatedValue") ?? "", context);
            if (decimal.TryParse(valRaw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var val))
                dto.EstimatedValue = val;

            var currency = ResolveVariables(GetNodeDataString(node, "currency") ?? "", context);
            if (!string.IsNullOrWhiteSpace(currency)) dto.Currency = currency;

            try
            {
                var created = await dealService.CreateDealAsync(dto, context.UserId ?? "workflow", "Workflow");
                _logger.LogInformation("[WORKFLOW] create-deal: created deal {DealId} '{Title}' (stage={Stage})",
                    created.Id, created.Title, created.Stage);
                return new NodeExecutionResult
                {
                    Success = true, Status = "completed",
                    CreatedEntityId = created.Id, CreatedEntityType = "deal",
                    Output = new Dictionary<string, object?>
                    {
                        ["action"] = "create_deal",
                        ["dealId"] = created.Id,
                        ["dealNumber"] = created.DealNumber,
                        ["title"] = created.Title,
                        ["stage"] = created.Stage,
                        ["contactId"] = created.ContactId,
                        ["estimatedValue"] = created.EstimatedValue,
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORKFLOW] create-deal failed");
                return new NodeExecutionResult { Success = false, Status = "failed", Error = $"create-deal failed: {ex.Message}" };
            }
        }

        private async Task<NodeExecutionResult> ExecuteUpdateDealStageAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var dealService = _serviceProvider.GetService(typeof(IDealService)) as IDealService;
            if (dealService == null)
            {
                return new NodeExecutionResult
                {
                    Success = false, Status = "failed",
                    Error = "IDealService is not registered — the Deals module must be enabled to use update-deal-status nodes."
                };
            }

            // Resolve target deal ID: explicit config → trigger (deal) → last-created deal.
            int dealId = 0;
            var idRaw = ResolveVariables(GetNodeDataString(node, "dealId") ?? GetNodeDataString(node, "entityId") ?? "", context).Trim();
            if (int.TryParse(idRaw, out var parsed) && parsed > 0)
                dealId = parsed;
            else if (context.TriggerEntityType == "deal")
                dealId = context.TriggerEntityId;
            else if (context.Variables.TryGetValue("created_deal_id", out var cd) && cd != null
                     && int.TryParse(cd.ToString(), out var cid))
                dealId = cid;

            if (dealId <= 0)
            {
                return new NodeExecutionResult
                {
                    Success = false, Status = "failed",
                    Error = "update-deal-status: could not determine dealId. Set node.data.dealId or trigger from a deal."
                };
            }

            var newStage = ResolveVariables(
                GetNodeDataString(node, "newStatus")
                ?? GetNodeDataString(node, "stage")
                ?? GetNodeDataString(node, "newStage")
                ?? "",
                context).Trim();

            if (string.IsNullOrWhiteSpace(newStage))
            {
                return new NodeExecutionResult
                {
                    Success = false, Status = "failed",
                    Error = "update-deal-status: 'newStatus' (or 'stage') is required."
                };
            }

            try
            {
                var updated = await dealService.UpdateDealAsync(dealId,
                    new UpdateDealDto { Stage = newStage },
                    context.UserId ?? "workflow", "Workflow");

                if (updated == null)
                {
                    return new NodeExecutionResult
                    {
                        Success = false, Status = "failed",
                        Error = $"update-deal-status: deal {dealId} not found."
                    };
                }

                _logger.LogInformation("[WORKFLOW] update-deal-status: deal {DealId} → {Stage}", dealId, newStage);
                return new NodeExecutionResult
                {
                    Success = true, Status = "completed",
                    Output = new Dictionary<string, object?>
                    {
                        ["action"] = "update_deal_status",
                        ["dealId"] = dealId,
                        ["newStage"] = newStage,
                        ["previousStage"] = updated.Stage,
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORKFLOW] update-deal-status failed");
                return new NodeExecutionResult { Success = false, Status = "failed", Error = $"update-deal-status failed: {ex.Message}" };
            }
        }

        private async Task<NodeExecutionResult> ExecuteUpdateStatusAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            _logger.LogInformation(
                "[WORKFLOW-UPDATE-STATUS] Executing update-status node: {NodeId} (Label: {Label}, Type: {Type})",
                node.Id, node.Label, node.Type);
            
            // Try explicit entityType from node data first, then infer from node.Type, then fall back to trigger context
            var entityType = GetNodeDataString(node, "entityType") 
                          ?? InferEntityTypeFromNodeType(node.Type) 
                          ?? context.TriggerEntityType;
            // Check multiple locations for newStatus (top-level and nested in config)
            var newStatus = GetNodeDataString(node, "newStatus") 
                         ?? GetNodeDataString(node, "status")
                         ?? GetConfigString(node, "newStatus")
                         ?? GetConfigString(node, "status");

            _logger.LogInformation(
                "[WORKFLOW-UPDATE-STATUS] Parsed: EntityType='{EntityType}', NewStatus='{NewStatus}', TriggerContext={TriggerType}#{TriggerId}",
                entityType, newStatus ?? "NULL", context.TriggerEntityType, context.TriggerEntityId);

            if (string.IsNullOrEmpty(newStatus))
            {
                _logger.LogError(
                    "[WORKFLOW-UPDATE-STATUS] No status specified in node {NodeId}. Cannot update.",
                    node.Id);
                return new NodeExecutionResult
                {
                    Success = false,
                    Status = "failed",
                    Error = "No status specified in update-status node"
                };
            }

            // CRITICAL: Resolve the correct entity ID for the target entity type
            // If we're updating a different entity than what triggered the workflow,
            // look up the correct ID from context variables (set by earlier create nodes)
            var entityId = context.TriggerEntityId;
            
            if (entityType != context.TriggerEntityType)
            {
                _logger.LogInformation(
                    "[WORKFLOW-UPDATE-STATUS] Target entity type '{TargetType}' differs from trigger type '{TriggerType}'. Resolving related ID...",
                    entityType, context.TriggerEntityType);
                
                // Check if a previous node created this entity type and stored its ID
                var createdKey = $"created_{entityType}_id";
                if (context.Variables.TryGetValue(createdKey, out var createdId) && createdId != null)
                {
                    if (createdId is int intId) entityId = intId;
                    else if (createdId is double dblId) entityId = (int)dblId;
                    else if (int.TryParse(createdId.ToString(), out var parsedId)) entityId = parsedId;
                    
                    _logger.LogInformation(
                        "[WORKFLOW-UPDATE-STATUS] Found created {EntityType} ID in context: {EntityId}",
                        entityType, entityId);
                }
                else
                {
                    // Try to find the related entity through DB relationships
                    var resolvedId = await ResolveRelatedEntityIdAsync(context.TriggerEntityType, context.TriggerEntityId, entityType);
                    _logger.LogInformation(
                        "[WORKFLOW-UPDATE-STATUS] Resolved {EntityType} ID via DB: {ResolvedId} (from {TriggerType}#{TriggerId})",
                        entityType, resolvedId, context.TriggerEntityType, context.TriggerEntityId);
                    entityId = resolvedId;
                }
            }

            _logger.LogInformation(
                "[WORKFLOW-UPDATE-STATUS] Updating {EntityType} #{EntityId} to status '{NewStatus}'",
                entityType, entityId, newStatus);

            // Update the entity status
            var success = entityType switch
            {
                "offer" => await UpdateOfferStatusAsync(entityId, newStatus, context.UserId),
                "sale" => await UpdateSaleStatusAsync(entityId, newStatus, context.UserId),
                "service_order" => await UpdateServiceOrderStatusAsync(entityId, newStatus, context.UserId),
                "dispatch" => await UpdateDispatchStatusAsync(entityId, newStatus, context.UserId),
                "job" => await UpdateJobStatusAsync(entityId, newStatus, context.UserId),
                "deal" => await UpdateDealStatusAsync(entityId, newStatus, context.UserId),
                _ => false
            };

            return new NodeExecutionResult
            {
                Success = success,
                Status = success ? "completed" : "failed",
                Error = success ? null : $"Failed to update {entityType} #{entityId} status to {newStatus}",
                Output = new Dictionary<string, object?>
                {
                    ["action"] = "update_status",
                    ["entityType"] = entityType,
                    ["entityId"] = entityId,
                    ["newStatus"] = newStatus
                }
            };
        }
        
        /// <summary>
        /// Gets a string value directly from node.data.config.{key}
        /// </summary>
        private string? GetConfigString(WorkflowNode node, string key)
        {
            if (node.Data.TryGetValue("config", out var configValue) && configValue is JsonElement configEl && configEl.ValueKind == JsonValueKind.Object)
            {
                if (configEl.TryGetProperty(key, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.String) return prop.GetString();
                    if (prop.ValueKind != JsonValueKind.Null && prop.ValueKind != JsonValueKind.Object && prop.ValueKind != JsonValueKind.Array)
                        return prop.ToString();
                }
            }
            return null;
        }

        #endregion

        #region Condition Nodes

        private async Task<NodeExecutionResult> ExecuteConditionNodeAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            _logger.LogInformation(
                "[WORKFLOW-CONDITION] Evaluating condition node: {NodeId} (Label: {Label})",
                node.Id, node.Label);
            
            // Log all node data for debugging
            _logger.LogInformation(
                "[WORKFLOW-CONDITION] Node data keys: [{Keys}]",
                string.Join(", ", node.Data.Keys));
            
            // Check multiple locations where condition fields could be stored:
            // 1. Top-level: node.data.field (from handlePanelConfigSave spreading)
            // 2. In config: node.data.config.field (NodeConfigPanel saves flat config)
            // 3. In config.condition: node.data.config.condition.field (ConditionalConfigModal saves nested)
            // 4. In config.conditionData: node.data.config.conditionData.field (seed data / DB format)
            var field = GetNodeDataString(node, "field") 
                     ?? GetNodeDataString(node, "conditionField")
                     ?? GetConditionNestedString(node, "field");
            var operatorType = GetNodeDataString(node, "operator") 
                            ?? GetConditionNestedString(node, "operator") 
                            ?? "equals";
            var expectedValue = GetNodeDataValue(node, "value") 
                             ?? GetNodeDataValue(node, "expectedValue")
                             ?? GetConditionNestedValue(node, "value");
            var checkField = GetConditionNestedString(node, "checkField"); // For array operations like all_match

            _logger.LogInformation(
                "[WORKFLOW-CONDITION] Parsed condition: Field='{Field}', Operator='{Operator}', ExpectedValue='{Expected}', CheckField='{CheckField}'",
                field ?? "NULL", operatorType, expectedValue ?? "NULL", checkField ?? "NULL");

            if (string.IsNullOrEmpty(field))
            {
                _logger.LogWarning(
                    "[WORKFLOW-CONDITION] No field specified for condition node {NodeId}. Defaulting to TRUE branch.",
                    node.Id);
                return new NodeExecutionResult
                {
                    Success = true,
                    Status = "completed",
                    SelectedBranch = "true", // Default to true branch if no condition
                    Output = new Dictionary<string, object?> { ["conditionResult"] = true }
                };
            }

            // Special handling for collection-based conditions
            bool result;
            object? actualValue;
            
            // Check if this is a collection field like "sale.items", "serviceOrder.dispatches", "sale.serviceOrders", "offer.sales"
            var fieldLower = field.ToLower();
            var isCollectionField = field.Contains('.') && 
                (fieldLower.EndsWith(".items") || fieldLower.EndsWith(".dispatches") || 
                 fieldLower.EndsWith(".serviceorders") || fieldLower.EndsWith(".service_orders") ||
                 fieldLower.EndsWith(".sales") || fieldLower.EndsWith(".jobs"));
            var isCollectionOperator = operatorType.ToLower() == "all_match" || 
                                       operatorType.ToLower() == "any_match" ||
                                       operatorType.ToLower() == "contains";
            
            if (isCollectionField && isCollectionOperator)
            {
                // For collection fields with collection operators, evaluate the collection directly
                result = await EvaluateCollectionConditionAsync(field, operatorType, expectedValue?.ToString(), checkField, context);
                actualValue = result; // The result is already the boolean
            }
            else
            {
                // Get the actual value from context or entity
                actualValue = await GetFieldValueAsync(field, context);
                // Evaluate the condition
                result = EvaluateCondition(actualValue, operatorType, expectedValue);
            }

            _logger.LogInformation(
                "[WORKFLOW-CONDITION] Condition evaluated: {Field} {Operator} {Expected} = {Result} (actual: {Actual}) -> Branch: {Branch}",
                field, operatorType, expectedValue, result, actualValue, result ? "YES/TRUE" : "NO/FALSE");

            return new NodeExecutionResult
            {
                Success = true,
                Status = "completed",
                SelectedBranch = result ? "yes" : "no",
                Output = new Dictionary<string, object?>
                {
                    ["conditionResult"] = result,
                    ["selectedBranch"] = result ? "yes" : "no",
                    ["field"] = field,
                    ["operator"] = operatorType,
                    ["expectedValue"] = expectedValue,
                    ["actualValue"] = actualValue
                }
            };
        }
        
        /// <summary>
        /// Evaluates collection conditions like all_match or any_match
        /// Example: serviceOrder.dispatches all_match status=technically_completed
        /// </summary>
        private async Task<bool> EvaluateCollectionConditionAsync(
            string field, 
            string operatorType, 
            string? expectedValue, 
            string? checkField,
            WorkflowExecutionContext context)
        {
            try
            {
                // Parse the field path (e.g., "serviceOrder.dispatches")
                if (!field.Contains('.'))
                    return false;
                    
                var parts = field.Split('.', 2);
                var entityPrefix = parts[0].ToLower();
                var collectionField = parts[1].ToLower();
                
                // Resolve the entity ID
                int entityId;
                // entityType tracked for future logging/extension
                
                if (entityPrefix == "serviceorder" || entityPrefix == "service_order")
                {
                    _ = "service_order"; // entityType for future use
                    // If triggered by dispatch, get the parent service order
                    if (context.TriggerEntityType == "dispatch")
                    {
                        var dispatch = await _db.Dispatches.FindAsync(context.TriggerEntityId);
                        if (dispatch?.ServiceOrderId == null) return false;
                        entityId = dispatch.ServiceOrderId.Value;
                    }
                    else if (context.TriggerEntityType == "service_order")
                    {
                        entityId = context.TriggerEntityId;
                    }
                    else
                    {
                        // Try to resolve from context variables
                        if (context.Variables.TryGetValue("created_service_order_id", out var soId) && soId is int id)
                            entityId = id;
                        else
                            entityId = await ResolveRelatedEntityIdAsync(context.TriggerEntityType, context.TriggerEntityId, "service_order");
                    }
                }
                else if (entityPrefix == "sale")
                {
                    _ = "sale"; // entityType for future use
                    // If triggered by sale, use the trigger entity ID directly
                    if (context.TriggerEntityType == "sale")
                    {
                        entityId = context.TriggerEntityId;
                    }
                    else if (context.TriggerEntityType == "service_order")
                    {
                        // Resolve sale from service order
                        entityId = await ResolveRelatedEntityIdAsync("service_order", context.TriggerEntityId, "sale");
                    }
                    else if (context.TriggerEntityType == "dispatch")
                    {
                        // Resolve sale from dispatch (via service order)
                        entityId = await ResolveRelatedEntityIdAsync("dispatch", context.TriggerEntityId, "sale");
                    }
                    else
                    {
                        // Try to resolve from context variables
                        if (context.Variables.TryGetValue("created_sale_id", out var saleIdVar) && saleIdVar is int sid)
                            entityId = sid;
                        else
                            entityId = context.TriggerEntityId; // Fallback
                    }
                    
                    _logger.LogInformation(
                        "EvaluateCollectionCondition: Resolved sale ID {SaleId} for sale.items check (trigger: {TriggerType}#{TriggerId})",
                        entityId, context.TriggerEntityType, context.TriggerEntityId);
                }
                else
                {
                    _logger.LogWarning(
                        "EvaluateCollectionCondition: Unknown entity prefix '{EntityPrefix}' in field '{Field}'",
                        entityPrefix, field);
                    return false; // Unknown entity type
                }
                
                // Handle the collection field
                if (collectionField == "dispatches")
                {
                    var dispatches = await _db.Dispatches
                        .Where(d => d.ServiceOrderId == entityId && !d.IsDeleted)
                        .ToListAsync();
                    
                    if (!dispatches.Any())
                    {
                        _logger.LogInformation("EvaluateCollectionCondition: No dispatches found for service order {ServiceOrderId}", entityId);
                        return false; // No items means condition fails
                    }
                    
                    // DYNAMIC: Get target statuses from the expectedValue (workflow configuration)
                    // The user configures what statuses to check in the workflow node
                    // Example: "technically_completed,completed" or just "completed"
                    var targetStatuses = !string.IsNullOrEmpty(expectedValue) 
                        ? expectedValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => s.Trim().ToLower())
                                       .ToArray()
                        : Array.Empty<string>();
                    
                    if (targetStatuses.Length == 0)
                    {
                        _logger.LogWarning("EvaluateCollectionCondition: No target statuses specified in workflow config for all_match check");
                        return false;
                    }
                    
                    var fieldToCheck = checkField?.ToLower() ?? "status";
                    
                    if (operatorType.ToLower() == "all_match")
                    {
                        if (fieldToCheck == "status")
                        {
                            var allMatch = dispatches.All(d => targetStatuses.Contains(d.Status?.ToLower() ?? ""));
                            _logger.LogInformation(
                                "EvaluateCollectionCondition: all_match check - {MatchCount}/{Total} dispatches match target statuses [{TargetStatuses}]. Result: {Result}",
                                dispatches.Count(d => targetStatuses.Contains(d.Status?.ToLower() ?? "")),
                                dispatches.Count,
                                string.Join(", ", targetStatuses),
                                allMatch);
                            return allMatch;
                        }
                    }
                    else if (operatorType.ToLower() == "any_match")
                    {
                        if (fieldToCheck == "status")
                        {
                            var anyMatch = dispatches.Any(d => targetStatuses.Contains(d.Status?.ToLower() ?? ""));
                            _logger.LogInformation(
                                "EvaluateCollectionCondition: any_match check - dispatches match target statuses [{TargetStatuses}]. Result: {Result}",
                                string.Join(", ", targetStatuses),
                                anyMatch);
                            return anyMatch;
                        }
                    }
                }
                else if (collectionField == "items")
                {
                    // Handle sale.items contains <type> check (e.g., "service", "article", "material")
                    if (entityPrefix == "sale")
                    {
                        var expectedType = expectedValue?.ToLower() ?? "service";
                        
                        _logger.LogInformation(
                            "EvaluateCollectionCondition: Checking if sale {SaleId} contains items of type '{ExpectedType}'",
                            entityId, expectedType);
                        
                        var hasItems = await _db.SaleItems.AnyAsync(si => 
                            si.SaleId == entityId && 
                            (
                                (si.Type != null && si.Type.ToLower() == expectedType) ||
                                (expectedType == "service" && si.RequiresServiceOrder)
                            ));
                        
                        _logger.LogInformation(
                            "EvaluateCollectionCondition: Sale {SaleId} contains items of type '{ExpectedType}': {Result}",
                            entityId, expectedType, hasItems);
                        
                        return hasItems;
                    }
                }
                else if (collectionField == "serviceorders" || collectionField == "service_orders")
                {
                    // Handle sale.serviceOrders all_match/any_match <status> check
                    if (entityPrefix == "sale")
                    {
                        var serviceOrders = await _db.ServiceOrders
                            .Where(so => so.SaleId == entityId.ToString())
                            .ToListAsync();
                        
                        if (!serviceOrders.Any())
                        {
                            _logger.LogInformation("EvaluateCollectionCondition: No service orders found for sale {SaleId}", entityId);
                            return false;
                        }
                        
                        var targetStatuses = !string.IsNullOrEmpty(expectedValue) 
                            ? expectedValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                           .Select(s => s.Trim().ToLower())
                                           .ToArray()
                            : Array.Empty<string>();
                        
                        if (targetStatuses.Length == 0) return false;
                        
                        var fieldToCheck = checkField?.ToLower() ?? "status";
                        
                        if (operatorType.ToLower() == "all_match")
                        {
                            if (fieldToCheck == "status")
                            {
                                var allMatch = serviceOrders.All(so => targetStatuses.Contains(so.Status?.ToLower() ?? ""));
                                _logger.LogInformation(
                                    "EvaluateCollectionCondition: all_match check - {MatchCount}/{Total} service orders match [{Statuses}]. Result: {Result}",
                                    serviceOrders.Count(so => targetStatuses.Contains(so.Status?.ToLower() ?? "")),
                                    serviceOrders.Count,
                                    string.Join(", ", targetStatuses),
                                    allMatch);
                                return allMatch;
                            }
                        }
                        else if (operatorType.ToLower() == "any_match" || operatorType.ToLower() == "contains")
                        {
                            if (fieldToCheck == "status")
                            {
                                var anyMatch = serviceOrders.Any(so => targetStatuses.Contains(so.Status?.ToLower() ?? ""));
                                _logger.LogInformation(
                                    "EvaluateCollectionCondition: any_match check - service orders match [{Statuses}]. Result: {Result}",
                                    string.Join(", ", targetStatuses),
                                    anyMatch);
                                return anyMatch;
                            }
                        }
                    }
                }
                else if (collectionField == "sales")
                {
                    // Handle offer.sales all_match/any_match <status> check
                    if (entityPrefix == "offer")
                    {
                        var sales = await _db.Sales
                            .Where(s => s.OfferId == entityId.ToString())
                            .ToListAsync();
                        
                        if (!sales.Any())
                        {
                            _logger.LogInformation("EvaluateCollectionCondition: No sales found for offer {OfferId}", entityId);
                            return false;
                        }
                        
                        var targetStatuses = !string.IsNullOrEmpty(expectedValue) 
                            ? expectedValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                           .Select(s => s.Trim().ToLower())
                                           .ToArray()
                            : Array.Empty<string>();
                        
                        if (targetStatuses.Length == 0) return false;
                        
                        var fieldToCheck = checkField?.ToLower() ?? "status";
                        
                        if (operatorType.ToLower() == "all_match")
                        {
                            if (fieldToCheck == "status")
                            {
                                var allMatch = sales.All(s => targetStatuses.Contains(s.Status?.ToLower() ?? ""));
                                _logger.LogInformation(
                                    "EvaluateCollectionCondition: all_match check - {MatchCount}/{Total} sales match [{Statuses}]. Result: {Result}",
                                    sales.Count(s => targetStatuses.Contains(s.Status?.ToLower() ?? "")),
                                    sales.Count,
                                    string.Join(", ", targetStatuses),
                                    allMatch);
                                return allMatch;
                            }
                        }
                        else if (operatorType.ToLower() == "any_match" || operatorType.ToLower() == "contains")
                        {
                            if (fieldToCheck == "status")
                            {
                                var anyMatch = sales.Any(s => targetStatuses.Contains(s.Status?.ToLower() ?? ""));
                                _logger.LogInformation(
                                    "EvaluateCollectionCondition: any_match check - sales match [{Statuses}]. Result: {Result}",
                                    string.Join(", ", targetStatuses),
                                    anyMatch);
                                return anyMatch;
                            }
                        }
                    }
                }
                else if (collectionField == "jobs")
                {
                    // Handle serviceOrder.jobs all_match/any_match <status> check
                    if (entityPrefix == "serviceorder" || entityPrefix == "service_order")
                    {
                        var jobs = await _db.ServiceOrderJobs
                            .Where(j => j.ServiceOrderId == entityId)
                            .ToListAsync();
                        
                        if (!jobs.Any())
                        {
                            _logger.LogInformation("EvaluateCollectionCondition: No jobs found for service order {ServiceOrderId}", entityId);
                            return false;
                        }
                        
                        var targetStatuses = !string.IsNullOrEmpty(expectedValue) 
                            ? expectedValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                           .Select(s => s.Trim().ToLower())
                                           .ToArray()
                            : Array.Empty<string>();
                        
                        if (targetStatuses.Length == 0) return false;
                        
                        var fieldToCheck = checkField?.ToLower() ?? "status";
                        
                        if (operatorType.ToLower() == "all_match")
                        {
                            if (fieldToCheck == "status")
                            {
                                var allMatch = jobs.All(j => targetStatuses.Contains(j.Status?.ToLower() ?? ""));
                                _logger.LogInformation(
                                    "EvaluateCollectionCondition: all_match check - {MatchCount}/{Total} jobs match [{Statuses}]. Result: {Result}",
                                    jobs.Count(j => targetStatuses.Contains(j.Status?.ToLower() ?? "")),
                                    jobs.Count,
                                    string.Join(", ", targetStatuses),
                                    allMatch);
                                return allMatch;
                            }
                        }
                        else if (operatorType.ToLower() == "any_match" || operatorType.ToLower() == "contains")
                        {
                            if (fieldToCheck == "status")
                            {
                                var anyMatch = jobs.Any(j => targetStatuses.Contains(j.Status?.ToLower() ?? ""));
                                _logger.LogInformation(
                                    "EvaluateCollectionCondition: any_match check - jobs match [{Statuses}]. Result: {Result}",
                                    string.Join(", ", targetStatuses),
                                    anyMatch);
                                return anyMatch;
                            }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EvaluateCollectionConditionAsync failed for field {Field}", field);
                return false;
            }
        }

        private async Task<NodeExecutionResult> ExecuteSwitchNodeAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var field = GetNodeDataString(node, "field") ?? GetNodeDataString(node, "switchField");
            
            if (string.IsNullOrEmpty(field))
            {
                return new NodeExecutionResult
                {
                    Success = false,
                    Status = "failed",
                    Error = "No field specified for switch node"
                };
            }

            var actualValue = await GetFieldValueAsync(field, context);
            var valueStr = actualValue?.ToString()?.ToLower() ?? "default";

            return new NodeExecutionResult
            {
                Success = true,
                Status = "completed",
                SelectedCase = valueStr,
                Output = new Dictionary<string, object?>
                {
                    ["switchField"] = field,
                    ["switchValue"] = actualValue,
                    ["selectedCase"] = valueStr
                }
            };
        }

        #endregion

        #region Action Nodes

        private async Task<NodeExecutionResult> ExecuteSendEmailAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var toRaw = GetNodeDataString(node, "to") ?? GetNodeDataString(node, "recipient") ?? GetNodeDataString(node, "recipientEmail");
            var subject = ResolveVariables(GetNodeDataString(node, "subject") ?? GetNodeDataString(node, "emailSubject") ?? "(no subject)", context);
            var body = ResolveVariables(GetNodeDataString(node, "body") ?? GetNodeDataString(node, "message") ?? GetNodeDataString(node, "template") ?? GetNodeDataString(node, "emailTemplate") ?? "", context);
            var fromName = GetNodeDataString(node, "fromName") ?? _configuration["Smtp:FromName"] ?? "Flowentra";
            var isHtml = (GetNodeDataString(node, "isHtml") ?? "true").ToLower() != "false";

            if (string.IsNullOrWhiteSpace(toRaw))
            {
                return new NodeExecutionResult { Success = false, Status = "failed", Error = "send-email: no recipient configured" };
            }
            var to = ResolveVariables(toRaw, context);

            // SMTP config: must be supplied via configuration / environment / secret store.
            // SECURITY FIX: no hardcoded credential fallbacks — those leaked a real mailbox password
            // into source control. Fail loudly when the SMTP transport is not configured.
            var host = _configuration["Smtp:Host"];
            var portRaw = _configuration["Smtp:Port"];
            var user = _configuration["Smtp:Username"];
            var pass = _configuration["Smtp:Password"];
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            {
                _logger.LogError("[WORKFLOW-EMAIL] SMTP is not configured (Smtp:Host / Smtp:Username / Smtp:Password). Skipping send-email node.");
                return new NodeExecutionResult { Success = false, Status = "failed", Error = "send-email: SMTP transport is not configured on the server" };
            }
            var port = int.TryParse(portRaw, out var p) ? p : 465;
            var useSsl = !(bool.TryParse(_configuration["Smtp:UseSsl"], out var ssl) && ssl == false);

            try
            {
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(fromName, user));
                foreach (var addr in to.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = addr.Trim();
                    if (trimmed.Length == 0) continue;
                    email.To.Add(MailboxAddress.Parse(trimmed));
                }
                if (email.To.Count == 0)
                {
                    return new NodeExecutionResult { Success = false, Status = "failed", Error = "send-email: no valid recipient addresses" };
                }
                email.Subject = subject;
                email.Body = new BodyBuilder { HtmlBody = isHtml ? body : null, TextBody = isHtml ? null : body }.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(host, port, useSsl);
                await client.AuthenticateAsync(user, pass);
                await client.SendAsync(email);
                await client.DisconnectAsync(true);

                _logger.LogInformation("[WORKFLOW-EMAIL] Sent to {To} subject='{Subject}'", to, subject);

                return new NodeExecutionResult
                {
                    Success = true,
                    Status = "completed",
                    Output = new Dictionary<string, object?>
                    {
                        ["action"] = "send_email",
                        ["to"] = to,
                        ["subject"] = subject,
                        ["status"] = "sent"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORKFLOW-EMAIL] Failed to send email to {To}", to);
                return new NodeExecutionResult { Success = false, Status = "failed", Error = $"send-email failed: {ex.Message}" };
            }
        }

        private async Task<NodeExecutionResult> ExecuteSendSmsAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var to = ResolveVariables(GetNodeDataString(node, "to") ?? GetNodeDataString(node, "phone") ?? "", context);
            var message = ResolveVariables(GetNodeDataString(node, "message") ?? "", context);

            if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(message))
            {
                return new NodeExecutionResult { Success = false, Status = "failed", Error = "send-sms: 'to' and 'message' are required" };
            }

            // Twilio is the only SMS transport we wire; if not configured, fail explicitly so the
            // workflow author sees the misconfiguration instead of a silent simulated success.
            var accountSid = _configuration["Twilio:AccountSid"];
            var authToken = _configuration["Twilio:AuthToken"];
            var from = _configuration["Twilio:From"];

            if (string.IsNullOrEmpty(accountSid) || string.IsNullOrEmpty(authToken) || string.IsNullOrEmpty(from))
            {
                _logger.LogWarning("[WORKFLOW-SMS] Twilio not configured (Twilio:AccountSid / AuthToken / From). Skipping send to {To}.", to);
                return new NodeExecutionResult
                {
                    Success = true,
                    Status = "completed",
                    Output = new Dictionary<string, object?>
                    {
                        ["action"] = "send_sms",
                        ["to"] = to,
                        ["status"] = "no_provider_configured",
                        ["hint"] = "Set Twilio:AccountSid, Twilio:AuthToken and Twilio:From in configuration to enable SMS."
                    }
                };
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("To", to),
                    new KeyValuePair<string, string>("From", from),
                    new KeyValuePair<string, string>("Body", message)
                });
                var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accountSid}:{authToken}"));
                var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
                var resp = await client.SendAsync(req);
                var respBody = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    return new NodeExecutionResult { Success = false, Status = "failed", Error = $"Twilio {(int)resp.StatusCode}: {respBody}" };
                }

                return new NodeExecutionResult
                {
                    Success = true,
                    Status = "completed",
                    Output = new Dictionary<string, object?>
                    {
                        ["action"] = "send_sms",
                        ["to"] = to,
                        ["status"] = "sent",
                        ["providerResponse"] = respBody
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORKFLOW-SMS] Twilio request failed");
                return new NodeExecutionResult { Success = false, Status = "failed", Error = $"send-sms failed: {ex.Message}" };
            }
        }

        private async Task<NodeExecutionResult> ExecuteNotificationAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var title = ResolveVariables(GetNodeDataString(node, "title") ?? GetNodeDataString(node, "notificationTitle") ?? "Workflow notification", context);
            var message = ResolveVariables(GetNodeDataString(node, "message") ?? GetNodeDataString(node, "notificationMessage") ?? "", context);
            var type = GetNodeDataString(node, "notificationType") ?? GetNodeDataString(node, "type") ?? "info";
            var category = GetNodeDataString(node, "category") ?? "workflow";
            var link = GetNodeDataString(node, "link");
            var recipients = GetNodeDataString(node, "recipients")
                          ?? GetNodeDataString(node, "userIds")
                          ?? GetNodeDataString(node, "recipientType")
                          ?? context.UserId;

            // Resolve target user IDs. Supports: explicit comma-separated user IDs,
            // "triggerUser" (the user that fired the workflow), or numeric value.
            var userIds = new List<int>();
            if (!string.IsNullOrWhiteSpace(recipients))
            {
                if (recipients.Equals("triggerUser", StringComparison.OrdinalIgnoreCase) ||
                    recipients.Equals("self", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(context.UserId, out var uid)) userIds.Add(uid);
                }
                else
                {
                    foreach (var raw in recipients.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (int.TryParse(raw.Trim(), out var uid)) userIds.Add(uid);
                    }
                }
            }

            if (userIds.Count == 0)
            {
                _logger.LogWarning("[WORKFLOW-NOTIFICATION] No resolvable user IDs from recipients='{R}' (context user='{U}')",
                    recipients, context.UserId);
                return new NodeExecutionResult
                {
                    Success = true,
                    Status = "completed",
                    Output = new Dictionary<string, object?>
                    {
                        ["action"] = "notification",
                        ["status"] = "no_recipients",
                        ["title"] = title
                    }
                };
            }

            int created = 0;
            foreach (var uid in userIds.Distinct())
            {
                try
                {
                    await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                    {
                        UserId = uid,
                        Title = title,
                        Description = message,
                        Type = type,
                        Category = category,
                        Link = link,
                        RelatedEntityId = context.TriggerEntityId,
                        RelatedEntityType = context.TriggerEntityType
                    });
                    created++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[WORKFLOW-NOTIFICATION] Failed to create notification for user {UserId}", uid);
                }
            }

            return new NodeExecutionResult
            {
                Success = true,
                Status = "completed",
                Output = new Dictionary<string, object?>
                {
                    ["action"] = "notification",
                    ["title"] = title,
                    ["recipients"] = userIds,
                    ["delivered"] = created
                }
            };
        }

        #endregion

        #region Integration Nodes

        private async Task<NodeExecutionResult> ExecuteApiCallAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var url = GetNodeDataString(node, "url");
            var method = GetNodeDataString(node, "httpMethod") ?? GetNodeDataString(node, "method") ?? "GET";
            var requestBody = GetNodeDataString(node, "requestBody");
            var customHeaders = GetNodeDataString(node, "customHeaders");
            var authType = GetNodeDataString(node, "authType") ?? "none";
            var timeout = GetNodeDataInt(node, "timeout") ?? 30;
            var contentType = GetNodeDataString(node, "contentType") ?? "json";
            var retryOnFailure = GetNodeDataBool(node, "retryOnFailure");
            var retryCount = GetNodeDataInt(node, "retryCount") ?? 3;
            var retryDelay = GetNodeDataInt(node, "retryDelay") ?? 1000;

            if (string.IsNullOrEmpty(url))
            {
                return new NodeExecutionResult
                {
                    Success = false,
                    Status = "failed",
                    Error = "URL is required for HTTP Request node"
                };
            }

            // Resolve variables in URL
            url = ResolveVariables(url, context);
            if (!string.IsNullOrEmpty(requestBody))
                requestBody = ResolveVariables(requestBody, context);

            // Append query params
            var queryParams = GetNodeDataString(node, "queryParams");
            if (!string.IsNullOrEmpty(queryParams))
            {
                var resolvedParams = ResolveVariables(queryParams, context);
                var paramPairs = resolvedParams.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Split(':', 2))
                    .Where(parts => parts.Length == 2)
                    .Select(parts => $"{Uri.EscapeDataString(parts[0].Trim())}={Uri.EscapeDataString(parts[1].Trim())}");
                var separator = url.Contains('?') ? "&" : "?";
                url = url + separator + string.Join("&", paramPairs);
            }

            var attempt = 0;
            var maxAttempts = retryOnFailure ? retryCount + 1 : 1;

            while (attempt < maxAttempts)
            {
                attempt++;
                try
                {
                    // PERF-6: reuse the pooled HttpMessageHandler via IHttpClientFactory
                    // instead of allocating a fresh HttpClient per attempt, which leaks
                    // sockets and exhausts ephemeral ports under load.
                    var httpClient = _httpClientFactory.CreateClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(timeout);

                    // Set auth headers
                    switch (authType)
                    {
                        case "bearer":
                            var token = ResolveVariables(GetNodeDataString(node, "bearerToken") ?? "", context);
                            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                            break;
                        case "basic":
                            var username = GetNodeDataString(node, "username") ?? "";
                            var password = GetNodeDataString(node, "password") ?? "";
                            var basicToken = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
                            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basicToken);
                            break;
                        case "api_key":
                            var headerName = GetNodeDataString(node, "apiKeyHeader") ?? "X-API-Key";
                            var apiKey = ResolveVariables(GetNodeDataString(node, "apiKeyValue") ?? "", context);
                            httpClient.DefaultRequestHeaders.Add(headerName, apiKey);
                            break;
                        case "oauth2":
                            var oauthToken = ResolveVariables(GetNodeDataString(node, "oauth2Token") ?? "", context);
                            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", oauthToken);
                            break;
                    }

                    // Set custom headers
                    if (!string.IsNullOrEmpty(customHeaders))
                    {
                        var resolvedHeaders = ResolveVariables(customHeaders, context);
                        foreach (var line in resolvedHeaders.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                        {
                            var parts = line.Split(':', 2);
                            if (parts.Length == 2)
                            {
                                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(parts[0].Trim(), parts[1].Trim());
                            }
                        }
                    }

                    // Build request
                    var httpMethod = new HttpMethod(method.ToUpper());
                    var request = new HttpRequestMessage(httpMethod, url);

                    if (!string.IsNullOrEmpty(requestBody) && method.ToUpper() != "GET")
                    {
                        var mediaType = contentType switch
                        {
                            "json" => "application/json",
                            "form" => "application/x-www-form-urlencoded",
                            "xml" => "application/xml",
                            "text" => "text/plain",
                            _ => "application/json"
                        };
                        request.Content = new StringContent(requestBody, System.Text.Encoding.UTF8, mediaType);
                    }

                    var response = await httpClient.SendAsync(request);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    _logger.LogInformation("HTTP {Method} {Url} → {StatusCode}", method, url, (int)response.StatusCode);

                    // Check success condition
                    var successCondition = GetNodeDataString(node, "successCondition") ?? "status_2xx";
                    var isSuccess = successCondition switch
                    {
                        "status_200" => response.StatusCode == System.Net.HttpStatusCode.OK,
                        "status_201" => response.StatusCode == System.Net.HttpStatusCode.Created,
                        "status_2xx" => response.IsSuccessStatusCode,
                        _ => response.IsSuccessStatusCode
                    };

                    // Parse response for mapping
                    object? parsedBody = responseBody;
                    try
                    {
                        parsedBody = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    }
                    catch { /* keep as string */ }

                    var output = new Dictionary<string, object?>
                    {
                        ["statusCode"] = (int)response.StatusCode,
                        ["body"] = parsedBody,
                        ["success"] = isSuccess,
                        ["url"] = url,
                        ["method"] = method
                    };

                    // Store headers if requested
                    var storeFullResponse = GetNodeDataBool(node, "storeFullResponse");
                    if (storeFullResponse)
                    {
                        output["headers"] = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));
                    }

                    return new NodeExecutionResult
                    {
                        Success = isSuccess,
                        Status = isSuccess ? "completed" : "failed",
                        Error = isSuccess ? null : $"HTTP {(int)response.StatusCode}: {responseBody.Substring(0, Math.Min(500, responseBody.Length))}",
                        Output = output
                    };
                }
                catch (TaskCanceledException)
                {
                    if (attempt >= maxAttempts)
                    {
                        return new NodeExecutionResult
                        {
                            Success = false,
                            Status = "failed",
                            Error = $"HTTP Request timed out after {timeout} seconds"
                        };
                    }
                    await Task.Delay(retryDelay);
                }
                catch (HttpRequestException ex)
                {
                    if (attempt >= maxAttempts)
                    {
                        return new NodeExecutionResult
                        {
                            Success = false,
                            Status = "failed",
                            Error = $"HTTP Request failed: {ex.Message}"
                        };
                    }
                    await Task.Delay(retryDelay);
                }
            }

            return new NodeExecutionResult
            {
                Success = false,
                Status = "failed",
                Error = "HTTP Request failed after all retries"
            };
        }

        private async Task<NodeExecutionResult> ExecuteWebhookAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var url = ResolveVariables(GetNodeDataString(node, "url") ?? GetNodeDataString(node, "webhookUrl") ?? "", context);
            var method = (GetNodeDataString(node, "method") ?? "POST").ToUpperInvariant();
            var bodyRaw = GetNodeDataString(node, "body") ?? GetNodeDataString(node, "payload");

            if (string.IsNullOrWhiteSpace(url))
            {
                return new NodeExecutionResult { Success = false, Status = "failed", Error = "webhook: 'url' is required" };
            }

            string body;
            if (!string.IsNullOrEmpty(bodyRaw))
            {
                body = ResolveVariables(bodyRaw, context);
            }
            else
            {
                // Default payload: full execution context envelope
                body = JsonSerializer.Serialize(new
                {
                    workflowId = context.WorkflowId,
                    executionId = context.ExecutionId,
                    entityType = context.TriggerEntityType,
                    entityId = context.TriggerEntityId,
                    userId = context.UserId,
                    variables = context.Variables,
                    triggeredAt = DateTime.UtcNow
                });
            }

            try
            {
                var client = _httpClientFactory.CreateClient("ext-webhook");
                var req = new HttpRequestMessage(new HttpMethod(method), url);
                if (method != "GET" && method != "DELETE")
                {
                    req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }

                // Optional auth header
                var authHeader = GetNodeDataString(node, "authorization");
                if (!string.IsNullOrEmpty(authHeader))
                {
                    req.Headers.TryAddWithoutValidation("Authorization", ResolveVariables(authHeader, context));
                }

                var resp = await client.SendAsync(req);
                var respBody = await resp.Content.ReadAsStringAsync();

                _logger.LogInformation("[WORKFLOW-WEBHOOK] {Method} {Url} -> {Status}", method, url, (int)resp.StatusCode);

                return new NodeExecutionResult
                {
                    Success = resp.IsSuccessStatusCode,
                    Status = resp.IsSuccessStatusCode ? "completed" : "failed",
                    Error = resp.IsSuccessStatusCode ? null : $"Webhook {(int)resp.StatusCode}: {respBody}",
                    Output = new Dictionary<string, object?>
                    {
                        ["action"] = "webhook",
                        ["url"] = url,
                        ["method"] = method,
                        ["statusCode"] = (int)resp.StatusCode,
                        ["responseBody"] = respBody
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORKFLOW-WEBHOOK] Request to {Url} failed", url);
                return new NodeExecutionResult { Success = false, Status = "failed", Error = $"webhook failed: {ex.Message}" };
            }
        }

        #endregion

        #region Approval Nodes

        private async Task<NodeExecutionResult> ExecuteApprovalAsync(WorkflowNode node, WorkflowExecutionContext context, int executionId)
        {
            var title = GetNodeDataString(node, "title") ?? "Approval Required";
            var message = GetNodeDataString(node, "message");
            var approverRole = GetNodeDataString(node, "approverRole") ?? "manager";
            var timeoutHours = GetNodeDataInt(node, "timeoutHours") ?? 24;

            // Create approval request
            var approval = new WorkflowApproval
            {
                ExecutionId = executionId,
                NodeId = node.Id,
                Title = title,
                Message = message,
                ApproverRole = approverRole,
                Status = "pending",
                TimeoutHours = timeoutHours,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(timeoutHours)
            };

            _db.WorkflowApprovals.Add(approval);
            await _db.SaveChangesAsync();

            return new NodeExecutionResult
            {
                Success = true,
                Status = "waiting_approval",
                ShouldStop = true, // Stop execution until approved
                Output = new Dictionary<string, object?>
                {
                    ["action"] = "approval",
                    ["approvalId"] = approval.Id,
                    ["title"] = title,
                    ["approverRole"] = approverRole,
                    ["expiresAt"] = approval.ExpiresAt
                }
            };
        }

        #endregion

        #region AI Nodes

        private async Task<NodeExecutionResult> ExecuteAiNodeAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var model  = GetNodeDataString(node, "model") ?? "openai/gpt-4o-mini";
            var prompt = ResolveVariables(GetNodeDataString(node, "prompt") ?? "", context);
            var system = ResolveVariables(GetNodeDataString(node, "systemPrompt") ?? "You are a helpful business assistant.", context);
            var aiType = node.Type.ToLower();

            _logger.LogInformation("[WORKFLOW-AI] Executing AI node: type={AiType}, model={Model}", aiType, model);

            // Resolve API key: node config → AppSettings DB row → configuration
            var apiKey = GetNodeDataString(node, "apiKey");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.SettingKey == "openrouter_api_key");
                apiKey = setting?.Value;
            }
            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = _configuration["OpenRouter:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new NodeExecutionResult
                {
                    Success = false,
                    Status = "failed",
                    Error = "OpenRouter API key not configured. Set it in Settings → App Settings (key: openrouter_api_key) or appsettings.json OpenRouter:ApiKey."
                };
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                return new NodeExecutionResult
                {
                    Success = false,
                    Status = "failed",
                    Error = "AI node: prompt is empty. Configure the prompt in the node settings."
                };
            }

            try
            {
                var client = _httpClientFactory.CreateClient("openrouter");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                client.DefaultRequestHeaders.Add("HTTP-Referer", "https://flowentra.app");
                client.DefaultRequestHeaders.Add("X-Title", "Flowentra Workflow");

                var requestBody = new
                {
                    model,
                    messages = new[]
                    {
                        new { role = "system", content = system },
                        new { role = "user",   content = prompt }
                    },
                    max_tokens = GetNodeDataInt(node, "maxTokens") ?? 1024,
                    temperature = GetNodeDataDouble(node, "temperature") ?? 0.7
                };

                var json    = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var cts      = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var httpResponse   = await client.PostAsync("https://openrouter.ai/api/v1/chat/completions", content, cts.Token);
                var responseText   = await httpResponse.Content.ReadAsStringAsync(cts.Token);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[WORKFLOW-AI] OpenRouter returned {Status}: {Body}", (int)httpResponse.StatusCode, responseText.Length > 500 ? responseText[..500] : responseText);
                    return new NodeExecutionResult
                    {
                        Success = false,
                        Status  = "failed",
                        Error   = $"OpenRouter API error ({(int)httpResponse.StatusCode}): {responseText}"
                    };
                }

                using var doc     = JsonDocument.Parse(responseText);
                var completion    = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "";

                var usage = doc.RootElement.TryGetProperty("usage", out var u) ? u : (JsonElement?)null;

                _logger.LogInformation("[WORKFLOW-AI] OpenRouter response: model={Model}, length={Len}", model, completion.Length);

                return new NodeExecutionResult
                {
                    Success = true,
                    Status  = "completed",
                    Output  = new Dictionary<string, object?>
                    {
                        ["action"]          = aiType.Contains("email") ? "ai_email_write" : "ai_generate",
                        ["model"]           = model,
                        ["result"]          = completion,
                        ["prompt_tokens"]   = usage?.TryGetProperty("prompt_tokens", out var pt) == true ? (int?)pt.GetInt32() : null,
                        ["completion_tokens"] = usage?.TryGetProperty("completion_tokens", out var ct2) == true ? (int?)ct2.GetInt32() : null,
                    }
                };
            }
            catch (TaskCanceledException)
            {
                return new NodeExecutionResult { Success = false, Status = "failed", Error = "AI node timed out after 60 seconds." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORKFLOW-AI] OpenRouter call failed");
                return new NodeExecutionResult { Success = false, Status = "failed", Error = $"AI node error: {ex.Message}" };
            }
        }

        #endregion

        #region Subworkflow Node

        private async Task<NodeExecutionResult> ExecuteSubWorkflowAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            // Guard against infinite recursion: max 5 levels deep
            var depth = context.Variables.TryGetValue("_subworkflow_depth", out var d) && d is int di ? di : 0;
            if (depth >= 5)
            {
                return new NodeExecutionResult
                {
                    Success = false,
                    Status = "failed",
                    Error = "subworkflow: maximum nesting depth (5) exceeded"
                };
            }

            // Resolve target workflow id
            var subworkflowIdRaw = GetNodeDataString(node, "workflowId")
                                ?? GetNodeDataString(node, "subworkflowId")
                                ?? GetNodeDataString(node, "targetWorkflowId");
            if (string.IsNullOrWhiteSpace(subworkflowIdRaw) || !int.TryParse(subworkflowIdRaw, out var subworkflowId))
            {
                return new NodeExecutionResult
                {
                    Success = false,
                    Status = "failed",
                    Error = "subworkflow: 'workflowId' is required and must be an integer"
                };
            }

            // Verify the target workflow exists and is active
            var targetWorkflow = await _db.WorkflowDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == subworkflowId && !w.IsDeleted);
            if (targetWorkflow == null)
            {
                return new NodeExecutionResult
                {
                    Success = false,
                    Status = "failed",
                    Error = $"subworkflow: workflow {subworkflowId} not found"
                };
            }

            // Build child variables: pass current variables + optional explicit mapping
            var childVariables = new Dictionary<string, object?>(context.Variables)
            {
                ["_subworkflow_depth"] = depth + 1,
                ["_parent_execution_id"] = context.ExecutionId,
                ["entityType"] = context.TriggerEntityType,
                ["entityId"] = context.TriggerEntityId,
            };

            // Explicit input mapping from node config: comma-separated "childVar=parentVar" or "childVar={{parentVar}}"
            var inputMapping = GetNodeDataString(node, "inputMapping");
            if (!string.IsNullOrWhiteSpace(inputMapping))
            {
                foreach (var line in inputMapping.Split('\n', ',', ';'))
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var childKey = parts[0].Trim();
                        var valueExpr = ResolveVariables(parts[1].Trim(), context);
                        if (!string.IsNullOrEmpty(childKey))
                            childVariables[childKey] = valueExpr;
                    }
                }
            }

            // Create a child execution record
            var childExecution = new WorkflowExecution
            {
                WorkflowId = subworkflowId,
                TriggerEntityType = context.TriggerEntityType,
                TriggerEntityId = context.TriggerEntityId,
                Status = "running",
                CurrentNodeId = "subworkflow-start",
                Context = JsonSerializer.Serialize(new
                {
                    parentExecutionId = context.ExecutionId,
                    parentNodeId = node.Id,
                    depth = depth + 1
                }),
                StartedAt = DateTime.UtcNow,
                TriggeredBy = context.UserId
            };
            _db.WorkflowExecutions.Add(childExecution);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "[WORKFLOW-SUB] Starting subworkflow {SubWorkflowId} (execution {ChildExecId}) from parent {ParentExecId}",
                subworkflowId, childExecution.Id, context.ExecutionId);

            // Lazily resolve IWorkflowGraphExecutor to avoid circular dependency
            var graphExecutor = _serviceProvider.GetRequiredService<IWorkflowGraphExecutor>();

            // Find the trigger/start node in the subworkflow
            var startNodeId = "trigger-1";
            try
            {
                var nodes = JsonSerializer.Deserialize<List<JsonElement>>(targetWorkflow.Nodes ?? "[]");
                var triggerNode = nodes?.FirstOrDefault(n =>
                {
                    if (n.TryGetProperty("type", out var typeEl))
                    {
                        var t = typeEl.GetString()?.ToLower() ?? "";
                        return t.Contains("trigger");
                    }
                    return false;
                });
                if (triggerNode.HasValue && triggerNode.Value.TryGetProperty("id", out var idEl))
                    startNodeId = idEl.GetString() ?? startNodeId;
            }
            catch { /* use default */ }

            var childContext = new WorkflowExecutionContext
            {
                WorkflowId = subworkflowId,
                ExecutionId = childExecution.Id,
                TriggerEntityType = context.TriggerEntityType,
                TriggerEntityId = context.TriggerEntityId,
                UserId = context.UserId,
                Variables = childVariables
            };

            GraphExecutionResult subResult;
            try
            {
                subResult = await graphExecutor.ExecuteGraphAsync(subworkflowId, childExecution.Id, startNodeId, childContext);
            }
            catch (Exception ex)
            {
                childExecution.Status = "failed";
                childExecution.Error = ex.Message;
                childExecution.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return new NodeExecutionResult
                {
                    Success = false,
                    Status = "failed",
                    Error = $"subworkflow execution failed: {ex.Message}"
                };
            }

            childExecution.Status = subResult.FinalStatus;
            childExecution.Error = subResult.Error;
            childExecution.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var success = subResult.FinalStatus == "completed";

            _logger.LogInformation(
                "[WORKFLOW-SUB] Subworkflow {SubWorkflowId} (execution {ChildExecId}) finished: status={Status}, nodes={Count}",
                subworkflowId, childExecution.Id, subResult.FinalStatus, subResult.NodesExecuted);

            // Expose subworkflow outputs to parent context
            var output = new Dictionary<string, object?>
            {
                ["subworkflow_id"]           = subworkflowId,
                ["subworkflow_execution_id"] = childExecution.Id,
                ["subworkflow_status"]       = subResult.FinalStatus,
                ["subworkflow_nodes"]        = subResult.NodesExecuted,
                ["subworkflow_error"]        = subResult.Error
            };

            // Also merge child node outputs into parent under "sub.<nodeId>.<key>"
            foreach (var kv in childContext.NodeOutputs)
                foreach (var ov in kv.Value is Dictionary<string, object?> d2 ? d2 : new Dictionary<string, object?>())
                    output[$"sub.{kv.Key}.{ov.Key}"] = ov.Value;

            return new NodeExecutionResult
            {
                Success = success,
                Status = success ? "completed" : "failed",
                Error = success ? null : subResult.Error,
                Output = output
            };
        }

        #endregion

        #region Loop Nodes

        private Task<NodeExecutionResult> ExecuteLoopNodeAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var loopType = GetNodeDataString(node, "loopType") ?? "for";
            var iterations = GetNodeDataInt(node, "iterations") ?? 1;

            _logger.LogInformation("Loop node executed: Type={LoopType}, Iterations={Iterations}", loopType, iterations);

            // Store loop metadata in context so downstream nodes can reference it
            context.Variables[$"{node.Id}_iteration"] = 0;
            context.Variables[$"{node.Id}_maxIterations"] = iterations;
            context.Variables[$"{node.Id}_loopType"] = loopType;

            // Note: Actual loop re-execution is handled by the graph executor
            // which checks if a node has loop semantics and re-queues children
            return Task.FromResult(new NodeExecutionResult
            {
                Success = true,
                Status = "completed",
                Output = new Dictionary<string, object?>
                {
                    ["action"] = "loop",
                    ["loopType"] = loopType,
                    ["iterations"] = iterations,
                    ["status"] = "loop_started"
                }
            });
        }

        #endregion

        #region Parallel & TryCatch Nodes

        private Task<NodeExecutionResult> ExecuteParallelNodeAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var maxConcurrency = GetNodeDataInt(node, "maxConcurrency") ?? 5;
            var waitForAll = GetNodeDataString(node, "waitForAll")?.ToLower() != "false";

            _logger.LogInformation("Parallel node executed: MaxConcurrency={Max}, WaitForAll={Wait}", maxConcurrency, waitForAll);

            // Store parallel settings in context for the graph executor
            context.Variables[$"{node.Id}_maxConcurrency"] = maxConcurrency;
            context.Variables[$"{node.Id}_waitForAll"] = waitForAll;

            return Task.FromResult(new NodeExecutionResult
            {
                Success = true,
                Status = "completed",
                Output = new Dictionary<string, object?>
                {
                    ["action"] = "parallel",
                    ["maxConcurrency"] = maxConcurrency,
                    ["waitForAll"] = waitForAll
                }
            });
        }

        private Task<NodeExecutionResult> ExecuteTryCatchNodeAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var retryCount = GetNodeDataInt(node, "retryCount") ?? 0;
            var onErrorAction = GetNodeDataString(node, "onErrorAction") ?? "stop";

            _logger.LogInformation("TryCatch node executed: RetryCount={Retry}, OnError={OnError}", retryCount, onErrorAction);

            context.Variables[$"{node.Id}_retryCount"] = retryCount;
            context.Variables[$"{node.Id}_onErrorAction"] = onErrorAction;

            return Task.FromResult(new NodeExecutionResult
            {
                Success = true,
                Status = "completed",
                Output = new Dictionary<string, object?>
                {
                    ["action"] = "try_catch",
                    ["retryCount"] = retryCount,
                    ["onErrorAction"] = onErrorAction
                }
            });
        }

        #region Contact Node

        private async Task<NodeExecutionResult> ExecuteContactNodeAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            // Contact nodes either look up or pass through contact info
            var contactId = GetNodeDataInt(node, "contactId") ?? context.TriggerEntityId;
            
            _logger.LogInformation("Contact node executed for contact #{ContactId}", contactId);

            // Try to load contact data from the trigger entity if it references a contact
            int? resolvedContactId = null;
            if (context.TriggerEntityType == "offer")
            {
                var offer = await _db.Offers.FindAsync(context.TriggerEntityId);
                resolvedContactId = offer?.ContactId;
            }
            else if (context.TriggerEntityType == "sale")
            {
                var sale = await _db.Sales.FindAsync(context.TriggerEntityId);
                resolvedContactId = sale?.ContactId;
            }
            else if (context.TriggerEntityType == "service_order")
            {
                var so = await _db.ServiceOrders.FindAsync(context.TriggerEntityId);
                resolvedContactId = so?.ContactId;
            }
            else if (context.TriggerEntityType == "dispatch")
            {
                var dispatch = await _db.Dispatches.FindAsync(context.TriggerEntityId);
                resolvedContactId = dispatch?.ContactId;
            }

            if (resolvedContactId.HasValue)
            {
                context.Variables["contact_id"] = resolvedContactId.Value;
            }

            return new NodeExecutionResult
            {
                Success = true,
                Status = "completed",
                Output = new Dictionary<string, object?>
                {
                    ["action"] = "contact_lookup",
                    ["contactId"] = resolvedContactId ?? contactId,
                    ["resolved"] = resolvedContactId.HasValue
                }
            };
        }

        #endregion


        private async Task<NodeExecutionResult> ExecuteDelayAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            // Support both legacy delayMs and new delayValue + delayUnit
            var delayValue = GetNodeDataInt(node, "delayValue");
            var delayUnit = GetNodeDataString(node, "delayUnit") ?? "minutes";
            var delayMode = GetNodeDataString(node, "delayMode") ?? "relative";

            // BUG FIX: use long arithmetic to avoid int overflow for delays >= ~25 days
            // (25 * 24 * 60 * 60 * 1000 > int.MaxValue → wrapped negative → Task.Delay throws).
            long delayMsLong;
            if (delayValue.HasValue)
            {
                long v = delayValue.Value;
                delayMsLong = delayUnit switch
                {
                    "seconds" => v * 1000L,
                    "minutes" => v * 60L * 1000L,
                    "hours"   => v * 60L * 60L * 1000L,
                    "days"    => v * 24L * 60L * 60L * 1000L,
                    _         => v * 60L * 1000L
                };
            }
            else
            {
                delayMsLong = GetNodeDataInt(node, "delayMs") ?? GetNodeDataInt(node, "delay") ?? 1000;
            }
            if (delayMsLong < 0) delayMsLong = 0;
            int delayMs = delayMsLong > int.MaxValue ? int.MaxValue : (int)delayMsLong;

            _logger.LogInformation(
                "Delay node: {Value} {Unit} (mode={Mode}, totalMs={Ms})",
                delayValue ?? delayMs, delayUnit, delayMode, delayMs);

            // Handle "wait until" mode
            if (delayMode == "until")
            {
                var waitUntil = GetNodeDataString(node, "waitUntilTime");
                if (!string.IsNullOrEmpty(waitUntil) && DateTime.TryParse(waitUntil, out var targetTime))
                {
                    var now = DateTime.UtcNow;
                    if (targetTime > now)
                    {
                        // BUG FIX (BUG-5): use long arithmetic + clamp; (int) cast of a
                        // TotalMilliseconds value >24 days wraps to a negative int and
                        // Task.Delay throws ArgumentOutOfRangeException.
                        var remainingMs = (long)(targetTime - now).TotalMilliseconds;
                        if (remainingMs < 0) remainingMs = 0;
                        delayMs = remainingMs > int.MaxValue ? int.MaxValue : (int)remainingMs;
                    }
                    else
                    {
                        delayMs = 0; // Already past
                    }
                }
            }

            // Short delays (≤ 30s) wait inline. Longer delays persist a ResumeAt timestamp on
            // the execution and stop the graph; WorkflowPollingService.PHASE 3 will resume the
            // graph after the delay node when ResumeAt is reached.
            if (delayMs <= 30000)
            {
                await Task.Delay(delayMs);
                return new NodeExecutionResult
                {
                    Success = true,
                    Status = "completed",
                    Output = new Dictionary<string, object?>
                    {
                        ["action"] = "delay",
                        ["delayMs"] = delayMs,
                        ["mode"] = "inline"
                    }
                };
            }

            var resumeAt = DateTime.UtcNow.AddMilliseconds(delayMs);
            try
            {
                var exec = await _db.WorkflowExecutions.FirstOrDefaultAsync(e => e.Id == context.ExecutionId);
                if (exec != null)
                {
                    exec.Status = "waiting_delay";
                    exec.WaitingNodeId = node.Id;
                    exec.CurrentNodeId = node.Id;
                    exec.ResumeAt = resumeAt;
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORKFLOW-DELAY] Failed to persist ResumeAt for execution {ExecutionId}", context.ExecutionId);
                return new NodeExecutionResult { Success = false, Status = "failed", Error = $"delay persist failed: {ex.Message}" };
            }

            _logger.LogInformation(
                "[WORKFLOW-DELAY] Execution {ExecutionId} parked on node {NodeId} until {ResumeAt:o}",
                context.ExecutionId, node.Id, resumeAt);

            return new NodeExecutionResult
            {
                Success = true,
                Status = "waiting_delay",
                ShouldStop = true,
                Output = new Dictionary<string, object?>
                {
                    ["action"] = "delay",
                    ["delayMs"] = delayMs,
                    ["resumeAt"] = resumeAt.ToString("O"),
                    ["mode"] = "scheduled"
                }
            };
        }

        private Task<NodeExecutionResult> ExecuteCalculationAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var expression = GetNodeDataString(node, "expression");
            var resultVar = GetNodeDataString(node, "resultVariable") ?? "result";

            if (string.IsNullOrWhiteSpace(expression))
            {
                return Task.FromResult(new NodeExecutionResult { Success = false, Status = "failed", Error = "calculate: 'expression' is required" });
            }

            // Substitute variables ({{name}} → numeric value) before handing off to DataTable.Compute,
            // which evaluates standard arithmetic expressions (+ - * / %, parentheses, IIF, etc.).
            var resolved = ResolveVariables(expression, context);

            try
            {
                using var table = new DataTable();
                var raw = table.Compute(resolved, string.Empty);
                object? value = raw;
                if (raw is IConvertible)
                {
                    try { value = Convert.ToDouble(raw, System.Globalization.CultureInfo.InvariantCulture); }
                    catch { /* keep raw */ }
                }

                context.Variables[resultVar] = value;

                return Task.FromResult(new NodeExecutionResult
                {
                    Success = true,
                    Status = "completed",
                    Output = new Dictionary<string, object?>
                    {
                        ["action"] = "calculate",
                        ["expression"] = expression,
                        ["resolvedExpression"] = resolved,
                        ["resultVariable"] = resultVar,
                        ["result"] = value
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new NodeExecutionResult
                {
                    Success = false,
                    Status = "failed",
                    Error = $"calculate failed for '{resolved}': {ex.Message}"
                });
            }
        }

        private Task<NodeExecutionResult> ExecuteSetVariableAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var name = GetNodeDataString(node, "name") ?? GetNodeDataString(node, "variableName");
            var value = GetNodeDataValue(node, "value");

            if (!string.IsNullOrEmpty(name))
            {
                context.Variables[name] = value;
            }

            return Task.FromResult(new NodeExecutionResult
            {
                Success = true,
                Status = "completed",
                Output = new Dictionary<string, object?>
                {
                    ["action"] = "set_variable",
                    ["name"] = name,
                    ["value"] = value
                }
            });
        }

        private Task<NodeExecutionResult> ExecuteDatabaseOperationAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var operation = GetNodeDataString(node, "operation") ?? "read";
            var table = GetNodeDataString(node, "table");

            // RELIABILITY FIX: the database node was a silent stub. Fail loudly so
            // workflows do not appear to succeed while no DB work happened.
            var message =
                $"database node is not implemented yet (operation={operation}, table={table ?? "?"}) — workflow halted to avoid a silent no-op.";
            _logger.LogError("[WORKFLOW] {Message}", message);
            return Task.FromResult(new NodeExecutionResult
            {
                Success = false,
                Status = "failed",
                Error = message,
                ShouldStop = true
            });
        }

        /// <summary>
        /// Executes a Code/JavaScript node.
        /// The code is evaluated server-side with access to the execution context variables.
        /// Uses a sandboxed approach: the code string is stored in node config, and the
        /// execution context (input from previous nodes + trigger data) is made available.
        /// </summary>
        private Task<NodeExecutionResult> ExecuteCodeNodeAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            _logger.LogInformation(
                "[WORKFLOW-CODE] Executing Code node: {NodeId} (Label: {Label})",
                node.Id, node.Label);

            var code = GetNodeDataString(node, "code") ?? GetConfigString(node, "code") ?? "";
            var continueOnError = GetNodeDataString(node, "continueOnError")?.ToLower() == "true"
                               || GetConfigString(node, "continueOnError")?.ToLower() == "true";
            var logOutput = GetNodeDataString(node, "logOutput")?.ToLower() != "false"
                         && GetConfigString(node, "logOutput")?.ToLower() != "false";
            var timeoutMs = GetNodeDataInt(node, "timeoutMs") ?? GetNodeDataInt(node, "timeout") ?? 2000;
            if (timeoutMs <= 0) timeoutMs = 2000;
            if (timeoutMs > 30000) timeoutMs = 30000; // hard cap: 30s

            if (string.IsNullOrWhiteSpace(code))
            {
                _logger.LogWarning("[WORKFLOW-CODE] Empty code in node {NodeId}. Passing through.", node.Id);
                return Task.FromResult(new NodeExecutionResult
                {
                    Success = true,
                    Status = "completed",
                    Output = new Dictionary<string, object?>
                    {
                        ["result"] = null,
                        ["logs"] = new List<string> { "No code provided — node passed through." },
                        ["error"] = (object?)null
                    }
                });
            }

            var logs = new List<string>();
            var sw = Stopwatch.StartNew();

            // Build the input object exposed to JS
            var input = new Dictionary<string, object?>();
            input["trigger"] = new Dictionary<string, object?>
            {
                ["entityType"] = context.TriggerEntityType,
                ["entityId"] = context.TriggerEntityId,
                ["status"] = context.Variables.TryGetValue("triggerStatus", out var ts) ? ts : null,
                ["fromStatus"] = context.Variables.TryGetValue("fromStatus", out var fs) ? fs : null,
                ["toStatus"] = context.Variables.TryGetValue("toStatus", out var tos) ? tos : null,
            };
            foreach (var kvp in context.Variables)
            {
                if (!input.ContainsKey(kvp.Key)) input[kvp.Key] = kvp.Value;
            }

            try
            {
                // SAFE SANDBOX: Jint with strict CPU/memory limits and no host bindings.
                // No AllowClr → user JS cannot access .NET types, the filesystem, or sockets.
                using var cts = new System.Threading.CancellationTokenSource(timeoutMs + 500);
                var engine = new Jint.Engine(options =>
                {
                    options.LimitRecursion(64);
                    options.MaxStatements(100_000);
                    options.TimeoutInterval(TimeSpan.FromMilliseconds(timeoutMs));
                    options.CancellationToken(cts.Token);
                });

                // Capturing console.log/warn/error — the only host binding we expose.
                var consoleObj = new Dictionary<string, object?>
                {
                    ["log"]   = new Action<object?>(o => { if (logs.Count < 500) logs.Add(o?.ToString() ?? "null"); }),
                    ["warn"]  = new Action<object?>(o => { if (logs.Count < 500) logs.Add("[warn] " + (o?.ToString() ?? "null")); }),
                    ["error"] = new Action<object?>(o => { if (logs.Count < 500) logs.Add("[error] " + (o?.ToString() ?? "null")); })
                };
                engine.SetValue("console", consoleObj);
                engine.SetValue("__input", JsonSerializer.Serialize(input));
                engine.SetValue("__context", JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["executionId"] = context.ExecutionId,
                    ["workflowId"]  = context.WorkflowId,
                    ["userId"]      = context.UserId,
                    ["timestamp"]   = DateTime.UtcNow.ToString("O")
                }));

                // Wrap user code so they can `return` from the top level. `input` and
                // `context` are parsed JSON snapshots — the user cannot mutate engine state.
                var wrapped = "(function(){ var input = JSON.parse(__input); var context = JSON.parse(__context); " + code + " })();";

                var jsResult = engine.Evaluate(wrapped);
                sw.Stop();

                object? resultValue = null;
                try { resultValue = jsResult.IsUndefined() || jsResult.IsNull() ? null : jsResult.ToObject(); }
                catch { resultValue = jsResult.ToString(); }

                if (logOutput)
                {
                    _logger.LogInformation(
                        "[WORKFLOW-CODE] Node {NodeId} completed in {Ms}ms ({LogCount} log lines)",
                        node.Id, sw.ElapsedMilliseconds, logs.Count);
                }

                // Persist the result for downstream nodes.
                context.Variables[$"{node.Id}_result"] = resultValue;

                return Task.FromResult(new NodeExecutionResult
                {
                    Success = true,
                    Status = "completed",
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    Output = new Dictionary<string, object?>
                    {
                        ["result"]     = resultValue,
                        ["logs"]       = logs,
                        ["error"]      = (object?)null,
                        ["executedAt"] = DateTime.UtcNow.ToString("O"),
                        ["durationMs"] = sw.ElapsedMilliseconds
                    }
                });
            }
            catch (Jint.Runtime.JavaScriptException jsEx)
            {
                sw.Stop();
                _logger.LogWarning("[WORKFLOW-CODE] JS error in node {NodeId}: {Msg}", node.Id, jsEx.Message);
                logs.Add($"JS error: {jsEx.Message}");
                if (continueOnError)
                {
                    return Task.FromResult(new NodeExecutionResult
                    {
                        Success = true, Status = "completed",
                        DurationMs = (int)sw.ElapsedMilliseconds,
                        Output = new Dictionary<string, object?> { ["result"] = null, ["logs"] = logs, ["error"] = jsEx.Message }
                    });
                }
                return Task.FromResult(new NodeExecutionResult
                {
                    Success = false, Status = "failed", ShouldStop = true,
                    Error = $"code node JS error: {jsEx.Message}",
                    DurationMs = (int)sw.ElapsedMilliseconds,
                    Output = new Dictionary<string, object?> { ["logs"] = logs, ["error"] = jsEx.Message }
                });
            }
            catch (Exception ex) when (ex is TimeoutException
                                    || ex is OperationCanceledException
                                    || ex.GetType().FullName?.StartsWith("Jint.Runtime.") == true)
            {
                sw.Stop();
                _logger.LogWarning(ex, "[WORKFLOW-CODE] Sandbox limit hit in node {NodeId}", node.Id);
                var msg = $"code sandbox limit exceeded ({ex.GetType().Name}): {ex.Message}";
                logs.Add(msg);
                if (continueOnError)
                {
                    return Task.FromResult(new NodeExecutionResult
                    {
                        Success = true, Status = "completed",
                        DurationMs = (int)sw.ElapsedMilliseconds,
                        Output = new Dictionary<string, object?> { ["result"] = null, ["logs"] = logs, ["error"] = msg }
                    });
                }
                return Task.FromResult(new NodeExecutionResult
                {
                    Success = false, Status = "failed", ShouldStop = true,
                    Error = msg, DurationMs = (int)sw.ElapsedMilliseconds,
                    Output = new Dictionary<string, object?> { ["logs"] = logs, ["error"] = msg }
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "[WORKFLOW-CODE] Unexpected error in code node {NodeId}", node.Id);
                if (continueOnError)
                {
                    return Task.FromResult(new NodeExecutionResult
                    {
                        Success = true, Status = "completed",
                        Output = new Dictionary<string, object?> { ["result"] = null, ["logs"] = new List<string> { $"Error (continued): {ex.Message}" }, ["error"] = ex.Message }
                    });
                }
                return Task.FromResult(new NodeExecutionResult
                {
                    Success = false, Status = "failed", ShouldStop = true,
                    Error = $"code node error: {ex.Message}",
                    Output = new Dictionary<string, object?> { ["error"] = ex.Message }
                });
            }
        }

        private Task<NodeExecutionResult> ExecuteDefaultNodeAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            _logger.LogInformation("Default execution for node type: {NodeType}", node.Type);

            return Task.FromResult(new NodeExecutionResult
            {
                Success = true,
                Status = "completed",
                Output = new Dictionary<string, object?>
                {
                    ["nodeType"] = node.Type,
                    ["status"] = "passed_through"
                }
            });
        }

        #endregion

        #region Helper Methods

        private string? GetNodeDataString(WorkflowNode node, string key)
        {
            // Check top-level data first
            if (node.Data.TryGetValue(key, out var value))
            {
                if (value is JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == JsonValueKind.String) return jsonElement.GetString();
                    if (jsonElement.ValueKind != JsonValueKind.Null && jsonElement.ValueKind != JsonValueKind.Object && jsonElement.ValueKind != JsonValueKind.Array)
                        return jsonElement.ToString();
                }
                else if (value != null)
                {
                    return value.ToString();
                }
            }
            
            // Fall back to nested "config" object (frontend stores configs inside node.data.config)
            if (node.Data.TryGetValue("config", out var configValue) && configValue is JsonElement configEl && configEl.ValueKind == JsonValueKind.Object)
            {
                if (configEl.TryGetProperty(key, out var configProp))
                {
                    if (configProp.ValueKind == JsonValueKind.String) return configProp.GetString();
                    if (configProp.ValueKind != JsonValueKind.Null && configProp.ValueKind != JsonValueKind.Object && configProp.ValueKind != JsonValueKind.Array)
                        return configProp.ToString();
                }
            }
            
            return null;
        }

        private object? GetNodeDataValue(WorkflowNode node, string key)
        {
            // Check top-level data first
            if (node.Data.TryGetValue(key, out var value))
            {
                if (value is JsonElement jsonElement)
                {
                    return jsonElement.ValueKind switch
                    {
                        JsonValueKind.String => jsonElement.GetString(),
                        JsonValueKind.Number => jsonElement.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => jsonElement.ToString()
                    };
                }
                return value;
            }
            
            // Fall back to nested "config" object
            if (node.Data.TryGetValue("config", out var configValue) && configValue is JsonElement configEl && configEl.ValueKind == JsonValueKind.Object)
            {
                if (configEl.TryGetProperty(key, out var configProp))
                {
                    return configProp.ValueKind switch
                    {
                        JsonValueKind.String => configProp.GetString(),
                        JsonValueKind.Number => configProp.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => configProp.ToString()
                    };
                }
            }
            
            return null;
        }

        private int? GetNodeDataInt(WorkflowNode node, string key)
        {
            var value = GetNodeDataValue(node, key);
            if (value is int i) return i;
            if (value is double d) return (int)d;
            if (value is string s && int.TryParse(s, out var parsed)) return parsed;
            return null;
        }

        private bool GetNodeDataBool(WorkflowNode node, string key)
        {
            var value = GetNodeDataValue(node, key);
            if (value is bool b) return b;
            if (value is string s) return s.Equals("true", StringComparison.OrdinalIgnoreCase);
            return false;
        }

        /// <summary>
        /// Resolves variable references in a template string using workflow context.
        /// Supported syntaxes:
        ///   {{varName}}          — flat variable from context.Variables
        ///   {{nodeId.key}}       — output key of a previously executed node
        ///   {{trigger.key}}      — same as {{key}}, convenience alias
        ///   ${varName}           — dollar-brace syntax used by the frontend editor
        ///   ${nodeId.key}        — dollar-brace node output reference
        /// Unresolved references are left as-is so callers can inspect them.
        /// </summary>
        private string ResolveVariables(string input, WorkflowExecutionContext context)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Inner resolver shared by both syntaxes.
            string Resolve(string expr)
            {
                // "nodeId.key" → NodeOutputs first, then Variables["nodeId.key"]
                var dotIdx = expr.IndexOf('.');
                if (dotIdx > 0)
                {
                    var nodeId = expr[..dotIdx];
                    var key    = expr[(dotIdx + 1)..];

                    // Special alias: {{trigger.xxx}} → context.Variables[xxx]
                    if (string.Equals(nodeId, "trigger", StringComparison.OrdinalIgnoreCase))
                    {
                        if (context.Variables.TryGetValue(key, out var tv) && tv != null)
                            return tv.ToString() ?? "";
                    }
                    else
                    {
                        // P0 FIX: the picker emits `{{label_slug.field}}` — translate the
                        // slug back to the real node ID so renaming a node doesn't silently
                        // break every downstream reference. Falls back to nodeId as-is
                        // for legacy `{{nodeId.field}}` references.
                        var resolvedNodeId = nodeId;
                        if (context.LabelSlugToNodeId.TryGetValue(nodeId, out var mapped))
                            resolvedNodeId = mapped;

                        if (context.NodeOutputs.TryGetValue(resolvedNodeId, out var outputs)
                            && outputs is Dictionary<string, object?> dict
                            && dict.TryGetValue(key, out var nv) && nv != null)
                            return nv.ToString() ?? "";

                        var fullKey = expr; // "nodeId.key"
                        if (context.Variables.TryGetValue(fullKey, out var fv) && fv != null)
                            return fv.ToString() ?? "";

                        _logger.LogWarning(
                            "[WORKFLOW-VAR] Unresolved reference '{{{{{Expr}}}}}' (node lookup key '{Key}') — check node label or that the referenced step ran before this one.",
                            expr, resolvedNodeId);
                    }
                    return $"{{{{{expr}}}}}"; // leave unresolved in {{}} form
                }

                // Flat variable
                if (context.Variables.TryGetValue(expr, out var flat) && flat != null)
                    return flat.ToString() ?? "";

                return $"{{{{{expr}}}}}"; // leave unresolved
            }

            // {{expr}} — double-brace syntax (covers trigger.x, nodeId.key, plain varName)
            input = System.Text.RegularExpressions.Regex.Replace(
                input, @"\{\{([\w.]+)\}\}", m => Resolve(m.Groups[1].Value));

            // ${expr} — dollar-brace syntax (frontend template strings)
            input = System.Text.RegularExpressions.Regex.Replace(
                input, @"\$\{([\w.]+)\}", m => Resolve(m.Groups[1].Value));

            return input;
        }

        /// <summary>
        /// Reads from config.condition.{key} - used by ConditionalConfigModal which stores
        /// if-else conditions as { condition: { field, operator, value } }
        /// </summary>
        private string? GetConditionNestedString(WorkflowNode node, string key)
        {
            if (node.Data.TryGetValue("config", out var configValue) && configValue is JsonElement configEl && configEl.ValueKind == JsonValueKind.Object)
            {
                // Check config.condition.{key} (ConditionalConfigModal format)
                if (configEl.TryGetProperty("condition", out var conditionEl) && conditionEl.ValueKind == JsonValueKind.Object)
                {
                    if (conditionEl.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                    {
                        return prop.GetString();
                    }
                }
                // Also check config.conditionData.{key} (seed data / DB format)
                if (configEl.TryGetProperty("conditionData", out var conditionDataEl) && conditionDataEl.ValueKind == JsonValueKind.Object)
                {
                    if (conditionDataEl.TryGetProperty(key, out var prop2) && prop2.ValueKind == JsonValueKind.String)
                    {
                        return prop2.GetString();
                    }
                }
            }
            return null;
        }

        private object? GetConditionNestedValue(WorkflowNode node, string key)
        {
            if (node.Data.TryGetValue("config", out var configValue) && configValue is JsonElement configEl && configEl.ValueKind == JsonValueKind.Object)
            {
                // Check config.condition.{key} (ConditionalConfigModal format)
                if (configEl.TryGetProperty("condition", out var conditionEl) && conditionEl.ValueKind == JsonValueKind.Object)
                {
                    if (conditionEl.TryGetProperty(key, out var prop))
                    {
                        return prop.ValueKind switch
                        {
                            JsonValueKind.String => prop.GetString(),
                            JsonValueKind.Number => prop.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop.ToString()
                        };
                    }
                }
                // Also check config.conditionData.{key} (seed data / DB format)
                if (configEl.TryGetProperty("conditionData", out var conditionDataEl) && conditionDataEl.ValueKind == JsonValueKind.Object)
                {
                    if (conditionDataEl.TryGetProperty(key, out var prop2))
                    {
                        return prop2.ValueKind switch
                        {
                            JsonValueKind.String => prop2.GetString(),
                            JsonValueKind.Number => prop2.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop2.ToString()
                        };
                    }
                }
            }
            return null;
        }


        private async Task<object?> GetFieldValueAsync(string field, WorkflowExecutionContext context)
        {
            _logger.LogInformation(
                "[WORKFLOW-FIELD] GetFieldValueAsync: field='{Field}', TriggerEntity={TriggerType}#{TriggerId}",
                field, context.TriggerEntityType, context.TriggerEntityId);
            
            // Check context variables first
            if (context.Variables.TryGetValue(field, out var varValue))
            {
                _logger.LogInformation("[WORKFLOW-FIELD] Found in context variables: {Value}", varValue);
                return varValue;
            }

            // Check node outputs
            if (context.NodeOutputs.TryGetValue(field, out var outputValue))
            {
                _logger.LogInformation("[WORKFLOW-FIELD] Found in node outputs: {Value}", outputValue);
                return outputValue;
            }

            // Strip entity prefix from dotted paths (e.g. "sale.hasServiceItems" → "hasServiceItems")
            var resolvedField = field;
            var resolvedEntityType = context.TriggerEntityType;
            
            if (field.Contains('.'))
            {
                var parts = field.Split('.', 2);
                var prefix = parts[0].ToLower();
                resolvedField = parts[1];
                
                // Override entity type if prefix specifies a different entity
                resolvedEntityType = prefix switch
                {
                    "offer" => "offer",
                    "sale" => "sale",
                    "serviceorder" or "service_order" => "service_order",
                    "dispatch" => "dispatch",
                    _ => context.TriggerEntityType
                };
                
                _logger.LogInformation(
                    "[WORKFLOW-FIELD] Parsed dotted path: prefix='{Prefix}' → entityType='{EntityType}', field='{Field}'",
                    prefix, resolvedEntityType, resolvedField);
            }

            // CRITICAL: Resolve the correct entity ID for the target entity type
            // If the dotted path references a different entity than the trigger, look up the right ID
            var resolvedEntityId = context.TriggerEntityId;
            if (resolvedEntityType != context.TriggerEntityType)
            {
                _logger.LogInformation(
                    "[WORKFLOW-FIELD] Need to resolve {TargetType} ID from {TriggerType}#{TriggerId}",
                    resolvedEntityType, context.TriggerEntityType, context.TriggerEntityId);
                
                var createdKey = $"created_{resolvedEntityType}_id";
                if (context.Variables.TryGetValue(createdKey, out var createdId) && createdId != null)
                {
                    if (createdId is int intId) resolvedEntityId = intId;
                    else if (createdId is double dblId) resolvedEntityId = (int)dblId;
                    else if (int.TryParse(createdId.ToString(), out var parsedId)) resolvedEntityId = parsedId;
                    
                    _logger.LogInformation("[WORKFLOW-FIELD] Found created ID in context: {EntityId}", resolvedEntityId);
                }
                else
                {
                    resolvedEntityId = await ResolveRelatedEntityIdAsync(context.TriggerEntityType, context.TriggerEntityId, resolvedEntityType);
                    _logger.LogInformation("[WORKFLOW-FIELD] Resolved via DB relationship: {EntityId}", resolvedEntityId);
                }
            }

            _logger.LogInformation(
                "[WORKFLOW-FIELD] Fetching {EntityType}#{EntityId}.{Field}",
                resolvedEntityType, resolvedEntityId, resolvedField);

            // Get from entity using the resolved ID
            var result = resolvedEntityType switch
            {
                "offer" => await GetOfferFieldAsync(resolvedEntityId, resolvedField),
                "sale" => await GetSaleFieldAsync(resolvedEntityId, resolvedField),
                "service_order" => await GetServiceOrderFieldAsync(resolvedEntityId, resolvedField),
                "dispatch" => await GetDispatchFieldAsync(resolvedEntityId, resolvedField),
                "deal" => await GetDealFieldAsync(resolvedEntityId, resolvedField),
                _ => null
            };
            
            _logger.LogInformation("[WORKFLOW-FIELD] Result: {Result}", result);
            return result;
        }

        private bool EvaluateCondition(object? actual, string op, object? expected)
        {
            var actualStr = actual?.ToString()?.ToLower() ?? "";
            var expectedStr = expected?.ToString()?.ToLower() ?? "";

            return op.ToLower() switch
            {
                "equals" or "==" or "eq" => actualStr == expectedStr,
                "not_equals" or "!=" or "neq" => actualStr != expectedStr,
                "contains" => actualStr.Contains(expectedStr),
                "not_contains" => !actualStr.Contains(expectedStr),
                "starts_with" => actualStr.StartsWith(expectedStr),
                "ends_with" => actualStr.EndsWith(expectedStr),
                "greater_than" or ">" or "gt" => CompareNumbers(actual, expected) > 0,
                "less_than" or "<" or "lt" => CompareNumbers(actual, expected) < 0,
                "greater_or_equal" or ">=" or "gte" => CompareNumbers(actual, expected) >= 0,
                "less_or_equal" or "<=" or "lte" => CompareNumbers(actual, expected) <= 0,
                "is_empty" => string.IsNullOrEmpty(actualStr),
                "is_not_empty" => !string.IsNullOrEmpty(actualStr),
                "is_true" => actualStr == "true" || actualStr == "1",
                "is_false" => actualStr == "false" || actualStr == "0",
                // For all_match / any_match, actual should already be pre-computed as bool
                "all_match" => actualStr == "true" || actualStr == "1",
                "any_match" => actualStr == "true" || actualStr == "1",
                _ => actualStr == expectedStr
            };
        }

        private int CompareNumbers(object? a, object? b)
        {
            if (double.TryParse(a?.ToString(), out var numA) && double.TryParse(b?.ToString(), out var numB))
            {
                return numA.CompareTo(numB);
            }
            return string.Compare(a?.ToString(), b?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private async Task<object?> GetOfferFieldAsync(int id, string field)
        {
            var offer = await _db.Offers.FindAsync(id);
            if (offer == null) return null;

            return field.ToLower() switch
            {
                "status" => offer.Status,
                "totalamount" or "total" => offer.TotalAmount,
                "grandtotal" => offer.GrandTotal,
                "contactid" => offer.ContactId,
                "validuntil" => offer.ValidUntil,
                _ => null
            };
        }

        private async Task<object?> GetDealFieldAsync(int id, string field)
        {
            var deal = await _db.Deals.FindAsync(id);
            if (deal == null) return null;

            return field.ToLower() switch
            {
                // "status" maps to the deal's stage so generic status conditions work.
                "status" or "stage" => deal.Stage,
                "probability" => deal.Probability,
                "estimatedvalue" or "value" => deal.EstimatedValue,
                "currency" => deal.Currency,
                "contactid" => deal.ContactId,
                "projectid" => deal.ProjectId,
                "category" => deal.Category,
                "source" => deal.Source,
                "title" => deal.Title,
                "expectedclosedate" => deal.ExpectedCloseDate,
                "nextactiondate" => deal.NextActionDate,
                "assignedto" => deal.AssignedTo,
                _ => null
            };
        }

        private async Task<object?> GetSaleFieldAsync(int id, string field)
        {
            var sale = await _db.Sales.FindAsync(id);
            if (sale == null) return null;

            return field.ToLower() switch
            {
                "status" => sale.Status,
                "totalamount" or "total" => sale.TotalAmount,
                "grandtotal" => sale.GrandTotal,
                "contactid" => sale.ContactId,
                "offerid" => sale.OfferId,
                // Check if sale has service items (items with Type == "service")
                "hasserviceitems" or "hasservices" => await _db.SaleItems
                    .AnyAsync(si => si.SaleId == id && si.Type == "service"),
                _ => null
            };
        }

        private async Task<object?> GetServiceOrderFieldAsync(int id, string field)
        {
            var serviceOrder = await _db.ServiceOrders.FindAsync(id);
            if (serviceOrder == null) return null;

            return field.ToLower() switch
            {
                "status" => serviceOrder.Status,
                "priority" => serviceOrder.Priority,
                "contactid" => serviceOrder.ContactId,
                "saleid" => serviceOrder.SaleId,
                "totalamount" => serviceOrder.TotalAmount,
                // Check if all dispatches for this service order are completed
                "alldispatchescompleted" or "alldone" or "dispatches" => await CheckAllDispatchesCompletedAsync(id),
                _ => null
            };
        }
        
        private async Task<bool> CheckAllDispatchesCompletedAsync(int serviceOrderId)
        {
            var dispatches = await _db.Dispatches
                .Where(d => d.ServiceOrderId == serviceOrderId && !d.IsDeleted)
                .Select(d => new { d.Id, d.Status })
                .ToListAsync();
            
            _logger.LogInformation(
                "[WORKFLOW-CHECK] CheckAllDispatchesCompleted for ServiceOrder #{ServiceOrderId}: Found {Count} dispatches",
                serviceOrderId, dispatches.Count);
            
            if (!dispatches.Any())
            {
                _logger.LogInformation("[WORKFLOW-CHECK] No dispatches found → returning FALSE");
                return false; // No dispatches means not completed
            }
            
            var completedStatuses = new[] { "technically_completed", "completed" };
            
            foreach (var d in dispatches)
            {
                var isComplete = completedStatuses.Contains(d.Status?.ToLower() ?? "");
                _logger.LogInformation(
                    "[WORKFLOW-CHECK] Dispatch #{DispatchId}: Status='{Status}', IsComplete={IsComplete}",
                    d.Id, d.Status, isComplete);
            }
            
            var allCompleted = dispatches.All(d => completedStatuses.Contains(d.Status?.ToLower() ?? ""));
            _logger.LogInformation("[WORKFLOW-CHECK] All dispatches completed: {Result}", allCompleted);
            
            return allCompleted;
        }

        private async Task<object?> GetDispatchFieldAsync(int id, string field)
        {
            var dispatch = await _db.Dispatches.FindAsync(id);
            if (dispatch == null) return null;

            return field.ToLower() switch
            {
                "status" => dispatch.Status,
                "priority" => dispatch.Priority,
                "contactid" => dispatch.ContactId,
                "serviceorderid" => dispatch.ServiceOrderId,
                "scheduleddate" => dispatch.ScheduledDate,
                _ => null
            };
        }

        /// <summary>
        /// Infer entity type from the node's business type string (e.g., "update-service-order-status" → "service_order")
        /// </summary>
        private static string? InferEntityTypeFromNodeType(string nodeType)
        {
            var lower = nodeType.ToLower();
            if (lower.Contains("service-order") || lower.Contains("service_order")) return "service_order";
            if (lower.Contains("dispatch")) return "dispatch";
            if (lower.Contains("job")) return "job";
            if (lower.Contains("sale")) return "sale";
            if (lower.Contains("offer")) return "offer";
            return null;
        }

        /// <summary>
        /// Resolves a related entity ID by traversing DB relationships.
        /// E.g., from dispatch → service_order, or sale → service_order.
        /// Supports multi-hop resolution (e.g., dispatch → service_order → sale)
        /// </summary>
        private async Task<int> ResolveRelatedEntityIdAsync(string fromEntityType, int fromEntityId, string toEntityType)
        {
            _logger.LogInformation(
                "[WORKFLOW-RESOLVE] Resolving {ToEntity} from {FromEntity}#{FromId}",
                toEntityType, fromEntityType, fromEntityId);
            
            try
            {
                // dispatch → service_order: look up dispatch.ServiceOrderId
                if (fromEntityType == "dispatch" && toEntityType == "service_order")
                {
                    var dispatch = await _db.Dispatches.FindAsync(fromEntityId);
                    var result = dispatch?.ServiceOrderId ?? 0;
                    _logger.LogInformation("[WORKFLOW-RESOLVE] dispatch→service_order: Found ServiceOrderId={Id}", result);
                    return result > 0 ? result : fromEntityId;
                }
                
                // dispatch → sale: go through service_order
                if (fromEntityType == "dispatch" && toEntityType == "sale")
                {
                    var dispatch = await _db.Dispatches.FindAsync(fromEntityId);
                    if (dispatch?.ServiceOrderId != null)
                    {
                        var so = await _db.ServiceOrders.FindAsync(dispatch.ServiceOrderId);
                        if (so?.SaleId != null && int.TryParse(so.SaleId, out var saleId))
                        {
                            _logger.LogInformation("[WORKFLOW-RESOLVE] dispatch→sale (via SO): Found SaleId={Id}", saleId);
                            return saleId;
                        }
                    }
                }
                
                // dispatch → offer: go through service_order → sale
                if (fromEntityType == "dispatch" && toEntityType == "offer")
                {
                    var dispatch = await _db.Dispatches.FindAsync(fromEntityId);
                    if (dispatch?.ServiceOrderId != null)
                    {
                        var so = await _db.ServiceOrders.FindAsync(dispatch.ServiceOrderId);
                        if (so?.SaleId != null && int.TryParse(so.SaleId, out var saleId))
                        {
                            var sale = await _db.Sales.FindAsync(saleId);
                            if (sale?.OfferId != null && int.TryParse(sale.OfferId, out var offerId))
                            {
                                _logger.LogInformation("[WORKFLOW-RESOLVE] dispatch→offer (via SO→Sale): Found OfferId={Id}", offerId);
                                return offerId;
                            }
                        }
                    }
                }
                
                // service_order → sale: look up service_order.SaleId
                if (fromEntityType == "service_order" && toEntityType == "sale")
                {
                    var so = await _db.ServiceOrders.FindAsync(fromEntityId);
                    if (so?.SaleId != null && int.TryParse(so.SaleId, out var saleId))
                    {
                        _logger.LogInformation("[WORKFLOW-RESOLVE] service_order→sale: Found SaleId={Id}", saleId);
                        return saleId;
                    }
                }
                
                // service_order → offer: go through sale
                if (fromEntityType == "service_order" && toEntityType == "offer")
                {
                    var so = await _db.ServiceOrders.FindAsync(fromEntityId);
                    if (so?.SaleId != null && int.TryParse(so.SaleId, out var saleId))
                    {
                        var sale = await _db.Sales.FindAsync(saleId);
                        if (sale?.OfferId != null && int.TryParse(sale.OfferId, out var offerId))
                        {
                            _logger.LogInformation("[WORKFLOW-RESOLVE] service_order→offer (via Sale): Found OfferId={Id}", offerId);
                            return offerId;
                        }
                    }
                }
                
                // sale → service_order: find SO with this SaleId
                if (fromEntityType == "sale" && toEntityType == "service_order")
                {
                    var so = await _db.ServiceOrders.FirstOrDefaultAsync(s => s.SaleId == fromEntityId.ToString());
                    if (so != null)
                    {
                        _logger.LogInformation("[WORKFLOW-RESOLVE] sale→service_order: Found ServiceOrderId={Id}", so.Id);
                        return so.Id;
                    }
                }
                
                // sale → dispatch: go through service_order
                if (fromEntityType == "sale" && toEntityType == "dispatch")
                {
                    var so = await _db.ServiceOrders.FirstOrDefaultAsync(s => s.SaleId == fromEntityId.ToString());
                    if (so != null)
                    {
                        var dispatch = await _db.Dispatches.FirstOrDefaultAsync(d => d.ServiceOrderId == so.Id && !d.IsDeleted);
                        if (dispatch != null)
                        {
                            _logger.LogInformation("[WORKFLOW-RESOLVE] sale→dispatch (via SO): Found DispatchId={Id}", dispatch.Id);
                            return dispatch.Id;
                        }
                    }
                }
                
                // service_order → dispatch: find first dispatch for this SO
                if (fromEntityType == "service_order" && toEntityType == "dispatch")
                {
                    var dispatch = await _db.Dispatches.FirstOrDefaultAsync(d => d.ServiceOrderId == fromEntityId && !d.IsDeleted);
                    if (dispatch != null)
                    {
                        _logger.LogInformation("[WORKFLOW-RESOLVE] service_order→dispatch: Found DispatchId={Id}", dispatch.Id);
                        return dispatch.Id;
                    }
                }
                
                // offer → sale: find sale from this offer
                if (fromEntityType == "offer" && toEntityType == "sale")
                {
                    var sale = await _db.Sales.FirstOrDefaultAsync(s => s.OfferId == fromEntityId.ToString());
                    if (sale != null)
                    {
                        _logger.LogInformation("[WORKFLOW-RESOLVE] offer→sale: Found SaleId={Id}", sale.Id);
                        return sale.Id;
                    }
                }
                
                // offer → service_order: go through sale
                if (fromEntityType == "offer" && toEntityType == "service_order")
                {
                    var sale = await _db.Sales.FirstOrDefaultAsync(s => s.OfferId == fromEntityId.ToString());
                    if (sale != null)
                    {
                        var so = await _db.ServiceOrders.FirstOrDefaultAsync(s => s.SaleId == sale.Id.ToString());
                        if (so != null)
                        {
                            _logger.LogInformation("[WORKFLOW-RESOLVE] offer→service_order (via Sale): Found ServiceOrderId={Id}", so.Id);
                            return so.Id;
                        }
                    }
                }
                
                // offer → dispatch: go through sale → service_order
                if (fromEntityType == "offer" && toEntityType == "dispatch")
                {
                    var sale = await _db.Sales.FirstOrDefaultAsync(s => s.OfferId == fromEntityId.ToString());
                    if (sale != null)
                    {
                        var so = await _db.ServiceOrders.FirstOrDefaultAsync(s => s.SaleId == sale.Id.ToString());
                        if (so != null)
                        {
                            var dispatch = await _db.Dispatches.FirstOrDefaultAsync(d => d.ServiceOrderId == so.Id && !d.IsDeleted);
                            if (dispatch != null)
                            {
                                _logger.LogInformation("[WORKFLOW-RESOLVE] offer→dispatch (via Sale→SO): Found DispatchId={Id}", dispatch.Id);
                                return dispatch.Id;
                            }
                        }
                    }
                }
                
                // sale → offer: look up sale.OfferId
                if (fromEntityType == "sale" && toEntityType == "offer")
                {
                    var sale = await _db.Sales.FindAsync(fromEntityId);
                    if (sale?.OfferId != null && int.TryParse(sale.OfferId, out var offerId))
                    {
                        _logger.LogInformation("[WORKFLOW-RESOLVE] sale→offer: Found OfferId={Id}", offerId);
                        return offerId;
                    }
                }
                
                _logger.LogWarning("[WORKFLOW-RESOLVE] No relationship found for {FromEntity}→{ToEntity}", fromEntityType, toEntityType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WORKFLOW-RESOLVE] Failed to resolve {ToEntity} from {FromEntity}#{FromId}", 
                    toEntityType, fromEntityType, fromEntityId);
            }
            
            // Fallback: return fromEntityId but log a clear warning
            // This will likely cause the update to fail gracefully (entity not found at that ID for the target type)
            _logger.LogWarning(
                "[WORKFLOW-RESOLVE] ⚠️ No relationship found from {FromEntity}#{FromId} → {ToEntity}. " +
                "Falling back to fromEntityId={FromId} which may target the WRONG entity. " +
                "Check workflow configuration and entity relationships.",
                fromEntityType, fromEntityId, toEntityType, fromEntityId);
            return fromEntityId;
        }

        private async Task<bool> UpdateOfferStatusAsync(int id, string newStatus, string? userId)
        {
            var offer = await _db.Offers.FindAsync(id);
            if (offer == null) return false;

            offer.Status = newStatus;
            offer.ModifiedDate = DateTime.UtcNow;
            offer.ModifiedBy = userId;
            await _db.SaveChangesAsync();
            return true;
        }

        private async Task<bool> UpdateDealStatusAsync(int id, string newStatus, string? userId)
        {
            // Deals track their pipeline state in "Stage", not "Status".
            var deal = await _db.Deals.FindAsync(id);
            if (deal == null)
            {
                _logger.LogError("[WORKFLOW-UPDATE-DEAL] Deal #{Id} not found!", id);
                return false;
            }

            var oldStage = deal.Stage;
            deal.Stage = newStatus;
            deal.ModifiedDate = DateTime.UtcNow;
            deal.ModifiedBy = userId;
            deal.LastActivity = DateTime.UtcNow;

            // Mirror the UI's close behaviour: stamp the close date on terminal stages.
            var lower = newStatus.ToLower();
            if ((lower == "won" || lower == "lost") && deal.ActualCloseDate == null)
            {
                deal.ActualCloseDate = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "[WORKFLOW-UPDATE-DEAL] Deal #{Id} stage '{OldStage}' -> '{NewStage}' (by {UserId})",
                id, oldStage, newStatus, userId ?? "system");

            return true;
        }

        private async Task<bool> UpdateJobStatusAsync(int id, string newStatus, string? userId)
        {
            var job = await _db.ServiceOrderJobs.FindAsync(id);
            if (job == null)
            {
                _logger.LogError("[WORKFLOW-UPDATE-JOB] Job #{Id} not found!", id);
                return false;
            }

            var oldStatus = job.Status;
            job.Status = newStatus;
            job.UpdatedAt = DateTime.UtcNow;

            var lower = newStatus.ToLower();
            if (lower == "completed" && !job.CompletedDate.HasValue)
            {
                job.CompletedDate = DateTime.UtcNow;
                if (!job.CompletionPercentage.HasValue || job.CompletionPercentage < 100)
                    job.CompletionPercentage = 100;
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "[WORKFLOW-UPDATE-JOB] Job #{Id} status '{OldStatus}' -> '{NewStatus}' (by {UserId})",
                id, oldStatus, newStatus, userId ?? "system");

            return true;
        }

        private async Task<bool> UpdateSaleStatusAsync(int id, string newStatus, string? userId)
        {
            var sale = await _db.Sales.FindAsync(id);
            if (sale == null) return false;

            sale.Status = newStatus;
            sale.ModifiedDate = DateTime.UtcNow;
            sale.ModifiedBy = userId;
            await _db.SaveChangesAsync();
            return true;
        }

        private async Task<bool> UpdateServiceOrderStatusAsync(int id, string newStatus, string? userId)
        {
            _logger.LogInformation(
                "[WORKFLOW-UPDATE-SO] Attempting to update ServiceOrder #{Id} to status '{NewStatus}' (by user: {UserId})",
                id, newStatus, userId ?? "system");
            
            var serviceOrder = await _db.ServiceOrders.FindAsync(id);
            if (serviceOrder == null)
            {
                _logger.LogError("[WORKFLOW-UPDATE-SO] ServiceOrder #{Id} not found!", id);
                return false;
            }

            var oldStatus = serviceOrder.Status;
            _logger.LogInformation(
                "[WORKFLOW-UPDATE-SO] ServiceOrder #{Id} current status: '{OldStatus}', changing to: '{NewStatus}' (length: {Len})",
                id, oldStatus, newStatus, newStatus.Length);
            
            serviceOrder.Status = newStatus;
            serviceOrder.ModifiedDate = DateTime.UtcNow;
            serviceOrder.ModifiedBy = userId;
            
            // Set ActualStartDate when status changes to in_progress
            var lowerNewStatus = newStatus.ToLower();
            if (lowerNewStatus == "in_progress" && !serviceOrder.ActualStartDate.HasValue)
            {
                serviceOrder.ActualStartDate = DateTime.UtcNow;
                _logger.LogInformation(
                    "[WORKFLOW-UPDATE-SO] Set ActualStartDate for ServiceOrder #{Id} (status: in_progress)",
                    id);
            }
            
            // DYNAMIC: Set completion timestamps based on status name patterns
            // The status names come from workflow configuration - users can define any status
            // We detect "completion" semantics by checking if status contains certain keywords
            var lowerStatus = newStatus.ToLower();
            var isCompletionStatus = lowerStatus.Contains("completed") || 
                                     lowerStatus.Contains("finished") || 
                                     lowerStatus.Contains("done") ||
                                     lowerStatus.Contains("closed");
            var isTechnicalCompletion = lowerStatus.Contains("technical") || lowerStatus.Contains("partial");
            
            if (isCompletionStatus)
            {
                _logger.LogInformation(
                    "[WORKFLOW-UPDATE-SO] Status '{Status}' is a completion status (technical: {IsTech})",
                    newStatus, isTechnicalCompletion);
                
                serviceOrder.ActualCompletionDate = DateTime.UtcNow;
                if (isTechnicalCompletion)
                {
                    serviceOrder.TechnicallyCompletedAt = DateTime.UtcNow;
                }
                
                // Count dispatches that are done, using the canonical definition shared with
                // ServiceOrderStatusCalculator. The previous fuzzy match (status contains
                // "completed"/"finished"/"done"/"closed") also matched non-terminal and
                // cancelled-ish statuses, so this counter disagreed with every other writer.
                var allDispatches = await _db.Dispatches
                    .Where(d => d.ServiceOrderId == id && !d.IsDeleted)
                    .ToListAsync();

                var completedCount = allDispatches.Count(d =>
                    MyApi.Modules.ServiceOrders.Services.ServiceOrderStatusCalculator.IsCompletedDispatchStatus(d.Status));

                serviceOrder.CompletedDispatchCount = completedCount;

                _logger.LogInformation(
                    "[WORKFLOW-UPDATE-SO] Updated CompletedDispatchCount to {Count} (out of {Total} dispatches)",
                    completedCount, allDispatches.Count);
            }
            
            try
            {
                _logger.LogInformation("[WORKFLOW-UPDATE-SO] Saving changes for ServiceOrder #{Id}...", id);
                await _db.SaveChangesAsync();
                _logger.LogInformation("[WORKFLOW-UPDATE-SO] ✅ Successfully saved ServiceOrder #{Id} with status '{NewStatus}'", id, newStatus);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex.InnerException ?? ex,
                    "[WORKFLOW-UPDATE-SO] ❌ Failed to save ServiceOrder #{Id}. " +
                    "Status: '{NewStatus}' (length: {Len}). " +
                    "DbUpdateException: {Message}. InnerException: {Inner}",
                    id, newStatus, newStatus.Length, ex.Message, ex.InnerException?.Message ?? "none");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[WORKFLOW-UPDATE-SO] ❌ Unexpected error saving ServiceOrder #{Id}: {Message}",
                    id, ex.Message);
                throw;
            }
            
            _logger.LogInformation(
                "[WORKFLOW-UPDATE-SO] Service order #{Id} status changed from '{Old}' to '{New}'",
                id, oldStatus, newStatus);
            
            return true;
        }

        private async Task<bool> UpdateDispatchStatusAsync(int id, string newStatus, string? userId)
        {
            var dispatch = await _db.Dispatches.FindAsync(id);
            if (dispatch == null) return false;

            var oldStatus = dispatch.Status;
            dispatch.Status = newStatus;
            dispatch.ModifiedDate = DateTime.UtcNow;
            dispatch.ModifiedBy = userId;
            
            // Set timestamps for status changes
            var lowerStatus = newStatus.ToLower();
            if (lowerStatus == "in_progress" && dispatch.ActualStartTime == null)
            {
                dispatch.ActualStartTime = DateTime.UtcNow;
            }
            if (lowerStatus.Contains("completed") || lowerStatus.Contains("finished") || lowerStatus.Contains("done"))
            {
                dispatch.ActualEndTime = DateTime.UtcNow;
            }
            
            await _db.SaveChangesAsync();
            
            // CRITICAL: Cascade status to parent Service Order via BusinessWorkflowService
            // This ensures ServiceOrder counters and status are updated correctly
            var isCompletionStatus = lowerStatus.Contains("completed") || lowerStatus.Contains("finished") || lowerStatus.Contains("done");
            var isInProgress = lowerStatus == "in_progress";
            
            if (isCompletionStatus)
            {
                _logger.LogInformation(
                    "UpdateDispatchStatusAsync: Dispatch #{Id} completed ({Status}), cascading to service order...",
                    id, newStatus);
                await _businessWorkflowService.HandleDispatchTechnicallyCompletedAsync(id, userId);
            }
            else if (isInProgress && oldStatus != "in_progress")
            {
                _logger.LogInformation(
                    "UpdateDispatchStatusAsync: Dispatch #{Id} in progress, cascading to service order...",
                    id);
                await _businessWorkflowService.HandleDispatchInProgressAsync(id, userId);
            }
            
            return true;
        }

        #endregion

        #region Dynamic Form, Data Transfer, Custom LLM Nodes

        private Task<NodeExecutionResult> ExecuteDynamicFormNodeAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var formId = GetNodeDataString(node, "formId");
            var formAction = GetNodeDataString(node, "formAction") ?? "collect";
            var assignTo = GetNodeDataString(node, "assignTo");

            _logger.LogInformation("Dynamic Form node: formId={FormId}, action={Action}, assignTo={AssignTo}", 
                formId, formAction, assignTo);

            switch (formAction)
            {
                case "collect":
                    // Pause workflow and wait for form submission
                    return Task.FromResult(new NodeExecutionResult
                    {
                        Success = true,
                        Status = "waiting_approval", // Reuse approval status to pause
                        Output = new Dictionary<string, object?>
                        {
                            ["action"] = "dynamic_form_collect",
                            ["formId"] = formId,
                            ["assignTo"] = assignTo,
                            ["awaitingSubmission"] = true
                        },
                        ShouldStop = true // Pause until form is submitted
                    });

                case "prefill":
                    var prefillMapping = GetNodeDataString(node, "prefillMapping");
                    var resolvedMapping = !string.IsNullOrEmpty(prefillMapping) 
                        ? ResolveVariables(prefillMapping, context) 
                        : "";

                    return Task.FromResult(new NodeExecutionResult
                    {
                        Success = true,
                        Status = "completed",
                        Output = new Dictionary<string, object?>
                        {
                            ["action"] = "dynamic_form_prefill",
                            ["formId"] = formId,
                            ["prefillData"] = resolvedMapping,
                            ["assignTo"] = assignTo
                        }
                    });

                case "read":
                    // Read last form response — in production, query the form responses table
                    return Task.FromResult(new NodeExecutionResult
                    {
                        Success = true,
                        Status = "completed",
                        Output = new Dictionary<string, object?>
                        {
                            ["action"] = "dynamic_form_read",
                            ["formId"] = formId,
                            ["responses"] = new Dictionary<string, object?>(), // Populated from DB in production
                            ["submittedBy"] = "system",
                            ["submittedAt"] = DateTime.UtcNow.ToString("O")
                        }
                    });

                default:
                    return Task.FromResult(new NodeExecutionResult
                    {
                        Success = false,
                        Status = "failed",
                        Error = $"Unknown form action: {formAction}"
                    });
            }
        }

        private Task<NodeExecutionResult> ExecuteDataTransferNodeAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var sourceModule = GetNodeDataString(node, "sourceModule") ?? "unknown";
            var operation = GetNodeDataString(node, "operation") ?? "read";
            var filter = GetNodeDataString(node, "filter");
            var dataMapping = GetNodeDataString(node, "dataMapping");

            if (!string.IsNullOrEmpty(filter))
                filter = ResolveVariables(filter, context);
            if (!string.IsNullOrEmpty(dataMapping))
                dataMapping = ResolveVariables(dataMapping, context);

            _logger.LogInformation("Data Transfer: module={Module}, op={Operation}, filter={Filter}", 
                sourceModule, operation, filter);

            // In production, this would query the appropriate module's service
            // For now, return a structured result that downstream nodes can consume
            return Task.FromResult(new NodeExecutionResult
            {
                Success = true,
                Status = "completed",
                Output = new Dictionary<string, object?>
                {
                    ["action"] = $"data_transfer_{operation}",
                    ["module"] = sourceModule,
                    ["operation"] = operation,
                    ["filter"] = filter,
                    ["dataMapping"] = dataMapping,
                    ["records"] = new List<object>(),
                    ["count"] = 0,
                    ["success"] = true
                }
            });
        }

        private async Task<NodeExecutionResult> ExecuteCustomLLMNodeAsync(WorkflowNode node, WorkflowExecutionContext context)
        {
            var providerName = GetNodeDataString(node, "providerName") ?? "Custom";
            var apiUrl = GetNodeDataString(node, "customApiUrl");
            var modelName = GetNodeDataString(node, "customModelName");
            var apiKey = GetNodeDataString(node, "customApiKey");
            var systemPrompt = GetNodeDataString(node, "systemPrompt") ?? "";
            var userPrompt = GetNodeDataString(node, "userPrompt") ?? "";
            var temperature = GetNodeDataDouble(node, "temperature") ?? 0.7;
            var maxTokens = GetNodeDataInt(node, "maxTokens") ?? 1000;

            if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(modelName))
            {
                return new NodeExecutionResult
                {
                    Success = false,
                    Status = "failed",
                    Error = "Custom LLM requires API URL and Model Name"
                };
            }

            // Resolve variables in prompts
            systemPrompt = ResolveVariables(systemPrompt, context);
            userPrompt = ResolveVariables(userPrompt, context);

            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                
                if (!string.IsNullOrEmpty(apiKey))
                {
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                }

                var requestBody = new
                {
                    model = modelName,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = temperature,
                    max_tokens = maxTokens
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(apiUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Custom LLM call failed: {Status} {Body}", (int)response.StatusCode, responseBody);
                    return new NodeExecutionResult
                    {
                        Success = false,
                        Status = "failed",
                        Error = $"LLM API returned {(int)response.StatusCode}: {responseBody.Substring(0, Math.Min(200, responseBody.Length))}"
                    };
                }

                // Parse OpenAI-compatible response
                var parsed = JsonSerializer.Deserialize<JsonElement>(responseBody);
                var outputText = "";
                var tokensUsed = 0;

                if (parsed.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) && 
                        message.TryGetProperty("content", out var c))
                    {
                        outputText = c.GetString() ?? "";
                    }
                }

                if (parsed.TryGetProperty("usage", out var usage) && 
                    usage.TryGetProperty("total_tokens", out var totalTokens))
                {
                    tokensUsed = totalTokens.GetInt32();
                }

                return new NodeExecutionResult
                {
                    Success = true,
                    Status = "completed",
                    Output = new Dictionary<string, object?>
                    {
                        ["output"] = outputText,
                        ["model"] = modelName,
                        ["provider"] = providerName,
                        ["tokensUsed"] = tokensUsed,
                        ["action"] = "custom_llm"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Custom LLM execution failed for provider {Provider}", providerName);
                return new NodeExecutionResult
                {
                    Success = false,
                    Status = "failed",
                    Error = $"Custom LLM error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Get a double value from node data
        /// </summary>
        private double? GetNodeDataDouble(WorkflowNode node, string key)
        {
            if (node.Data.TryGetValue(key, out var val) && val != null)
            {
                if (val is double d) return d;
                if (val is float f) return f;
                if (val is int i) return i;
                if (val is JsonElement je)
                {
                    if (je.ValueKind == JsonValueKind.Number) return je.GetDouble();
                }
                if (double.TryParse(val.ToString(), out var parsed)) return parsed;
            }
            return null;
        }

        #endregion
    }
}
