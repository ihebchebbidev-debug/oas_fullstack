namespace MyApi.Modules.Shared.Models
{
    public interface ISoftDeletable 
    {
        bool IsDeleted { get; set; }
        DateTime? DeletedAt { get; set; }
        string? DeletedBy { get; set; }
    }
}
