namespace MyApi.Modules.WorkflowEngine.DTOs
{
    /// <summary>
    /// DTO sent when a workflow execution starts.
    /// </summary>
    public class WorkflowExecutionStartedDto
    {
        public int WorkflowId { get; set; }
        public int ExecutionId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public DateTime StartedAt { get; set; }
        public string? TriggeredBy { get; set; }
    }

    /// <summary>
    /// DTO sent when a workflow node starts executing.
    /// </summary>
    public class WorkflowNodeExecutingDto
    {
        public int WorkflowId { get; set; }
        public int ExecutionId { get; set; }
        public string NodeId { get; set; } = string.Empty;
        public string NodeType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// DTO sent when a workflow node completes execution.
    /// </summary>
    public class WorkflowNodeCompletedDto
    {
        public int WorkflowId { get; set; }
        public int ExecutionId { get; set; }
        public string NodeId { get; set; } = string.Empty;
        public string NodeType { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? Output { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// DTO sent when a workflow execution completes.
    /// </summary>
    public class WorkflowExecutionCompletedDto
    {
        public int WorkflowId { get; set; }
        public int ExecutionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CompletedAt { get; set; }
        public int NodesExecuted { get; set; }
        public int NodesFailed { get; set; }
    }

    /// <summary>
    /// DTO sent when a workflow execution fails.
    /// </summary>
    public class WorkflowExecutionErrorDto
    {
        public int WorkflowId { get; set; }
        public int ExecutionId { get; set; }
        public string NodeId { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
