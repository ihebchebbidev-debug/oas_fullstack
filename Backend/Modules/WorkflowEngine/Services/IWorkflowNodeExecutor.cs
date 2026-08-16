namespace MyApi.Modules.WorkflowEngine.Services
{
    /// <summary>
    /// Executes individual workflow nodes based on their type
    /// </summary>
    public interface IWorkflowNodeExecutor
    {
        /// <summary>
        /// Execute a single node and return the result
        /// </summary>
        Task<NodeExecutionResult> ExecuteNodeAsync(
            int executionId,
            WorkflowNode node,
            WorkflowExecutionContext context);

        /// <summary>
        /// Check if a condition node evaluates to true
        /// </summary>
        Task<bool> EvaluateConditionAsync(
            WorkflowNode node,
            WorkflowExecutionContext context);
    }

    /// <summary>
    /// Represents a node in the workflow graph
    /// </summary>
    public class WorkflowNode
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public Dictionary<string, object?> Data { get; set; } = new();
        public NodePosition Position { get; set; } = new();
    }

    public class NodePosition
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    /// <summary>
    /// Represents an edge connecting two nodes
    /// </summary>
    public class WorkflowEdge
    {
        public string Id { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string? SourceHandle { get; set; }
        public string? TargetHandle { get; set; }
        public string? Label { get; set; }
    }

    /// <summary>
    /// Context passed between nodes during execution
    /// </summary>
    public class WorkflowExecutionContext
    {
        public int WorkflowId { get; set; }
        public int ExecutionId { get; set; }
        public string TriggerEntityType { get; set; } = string.Empty;
        public int TriggerEntityId { get; set; }
        public string? UserId { get; set; }
        public Dictionary<string, object?> Variables { get; set; } = new();
        public Dictionary<string, object?> NodeOutputs { get; set; } = new();
        /// <summary>
        /// Maps a slugified node label (lowercase, whitespace → underscore) to the
        /// real node ID. Populated once per execution by <c>WorkflowGraphExecutor</c>
        /// so the variable resolver can honour references like
        /// <c>{{welcome_email.subject}}</c> even after the user renames a node.
        /// </summary>
        public Dictionary<string, string> LabelSlugToNodeId { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Result of executing a single node
    /// </summary>
    public class NodeExecutionResult
    {
        public bool Success { get; set; }
        public string Status { get; set; } = "completed"; // 'completed', 'failed', 'skipped', 'waiting_approval'
        public string? Error { get; set; }
        public Dictionary<string, object?> Output { get; set; } = new();
        public int DurationMs { get; set; }
        
        /// <summary>
        /// For condition nodes - which branch to take
        /// </summary>
        public string? SelectedBranch { get; set; }
        
        /// <summary>
        /// For switch nodes - which case matched
        /// </summary>
        public string? SelectedCase { get; set; }
        
        /// <summary>
        /// If true, stop executing subsequent nodes
        /// </summary>
        public bool ShouldStop { get; set; }
        
        /// <summary>
        /// Entity ID created by this node (e.g., new sale ID)
        /// </summary>
        public int? CreatedEntityId { get; set; }
        
        /// <summary>
        /// Entity type created (e.g., "sale", "service_order")
        /// </summary>
        public string? CreatedEntityType { get; set; }
    }
}
