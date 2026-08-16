namespace MyApi.Modules.Shared.Domain.Common
{
    /// <summary>
    /// Base entity with audit fields
    /// </summary>
    public abstract class BaseEntity
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }
    }

    /// <summary>
    /// Base entity with soft delete functionality
    /// </summary>
    public abstract class BaseEntityWithSoftDelete : BaseEntity
    {
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }

    /// <summary>
    /// Base entity with active/inactive status
    /// </summary>
    public abstract class BaseEntityWithStatus : BaseEntityWithSoftDelete
    {
        public bool IsActive { get; set; } = true;
    }
}
