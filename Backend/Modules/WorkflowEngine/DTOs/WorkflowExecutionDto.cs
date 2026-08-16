using System.Text.Json.Serialization;

namespace MyApi.Modules.WorkflowEngine.DTOs
{
    public class WorkflowExecutionDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("workflowId")]
        public int WorkflowId { get; set; }

        [JsonPropertyName("workflowName")]
        public string? WorkflowName { get; set; }

        [JsonPropertyName("triggerEntityType")]
        public string TriggerEntityType { get; set; } = string.Empty;

        [JsonPropertyName("triggerEntityId")]
        public int TriggerEntityId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("currentNodeId")]
        public string? CurrentNodeId { get; set; }

        [JsonPropertyName("context")]
        public object? Context { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("startedAt")]
        public DateTime StartedAt { get; set; }

        [JsonPropertyName("completedAt")]
        public DateTime? CompletedAt { get; set; }

        [JsonPropertyName("triggeredBy")]
        public string? TriggeredBy { get; set; }

        [JsonPropertyName("duration")]
        public long? Duration { get; set; } // milliseconds

        [JsonPropertyName("logs")]
        public List<WorkflowExecutionLogDto> Logs { get; set; } = new();
    }

    public class WorkflowExecutionLogDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("nodeId")]
        public string NodeId { get; set; } = string.Empty;

        [JsonPropertyName("nodeType")]
        public string NodeType { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("input")]
        public object? Input { get; set; }

        [JsonPropertyName("output")]
        public object? Output { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("duration")]
        public int? Duration { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
    }

    public class WorkflowApprovalDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("executionId")]
        public int ExecutionId { get; set; }

        [JsonPropertyName("nodeId")]
        public string NodeId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("approverRole")]
        public string ApproverRole { get; set; } = string.Empty;

        [JsonPropertyName("approvedById")]
        public string? ApprovedById { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("responseNote")]
        public string? ResponseNote { get; set; }

        [JsonPropertyName("timeoutHours")]
        public int TimeoutHours { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("respondedAt")]
        public DateTime? RespondedAt { get; set; }

        [JsonPropertyName("expiresAt")]
        public DateTime? ExpiresAt { get; set; }

        [JsonPropertyName("workflowName")]
        public string? WorkflowName { get; set; }

        [JsonPropertyName("entityType")]
        public string? EntityType { get; set; }

        [JsonPropertyName("entityId")]
        public int? EntityId { get; set; }
    }

    public class ApprovalResponseDto
    {
        [JsonPropertyName("approved")]
        public bool Approved { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }
}
