using System.Text.Json.Serialization;

namespace MyApi.Modules.WorkflowEngine.DTOs
{
    public class WorkflowDefinitionDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("nodes")]
        public object Nodes { get; set; } = new List<object>();

        [JsonPropertyName("edges")]
        public object Edges { get; set; } = new List<object>();

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("createdBy")]
        public string? CreatedBy { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [JsonPropertyName("triggersCount")]
        public int TriggersCount { get; set; }

        [JsonPropertyName("executionsCount")]
        public int ExecutionsCount { get; set; }

        /// <summary>
        /// Full list of registered triggers for this workflow
        /// </summary>
        [JsonPropertyName("triggers")]
        public List<WorkflowTriggerDto> Triggers { get; set; } = new();
    }

    public class CreateWorkflowDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("nodes")]
        public object Nodes { get; set; } = new List<object>();

        [JsonPropertyName("edges")]
        public object Edges { get; set; } = new List<object>();

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;
    }

    public class UpdateWorkflowDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("nodes")]
        public object? Nodes { get; set; }

        [JsonPropertyName("edges")]
        public object? Edges { get; set; }

        [JsonPropertyName("isActive")]
        public bool? IsActive { get; set; }
    }

    public class WorkflowTriggerDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("workflowId")]
        public int WorkflowId { get; set; }

        [JsonPropertyName("nodeId")]
        public string NodeId { get; set; } = string.Empty;

        [JsonPropertyName("entityType")]
        public string EntityType { get; set; } = string.Empty;

        [JsonPropertyName("fromStatus")]
        public string? FromStatus { get; set; }

        [JsonPropertyName("toStatus")]
        public string? ToStatus { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }
    }

    public class RegisterTriggerDto
    {
        [JsonPropertyName("workflowId")]
        public int WorkflowId { get; set; }

        [JsonPropertyName("nodeId")]
        public string NodeId { get; set; } = string.Empty;

        [JsonPropertyName("entityType")]
        public string EntityType { get; set; } = string.Empty;

        [JsonPropertyName("fromStatus")]
        public string? FromStatus { get; set; }

        [JsonPropertyName("toStatus")]
        public string? ToStatus { get; set; }
    }
}
