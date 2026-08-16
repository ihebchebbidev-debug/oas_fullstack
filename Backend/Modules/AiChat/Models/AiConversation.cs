using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.AiChat.Models
{
    [Table("AiConversations")]
    public class AiConversation : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = "New Conversation";

        public string? Summary { get; set; }

        public DateTime? LastMessageAt { get; set; }

        public int MessageCount { get; set; } = 0;

        public bool IsArchived { get; set; } = false;

        public bool IsPinned { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? DeletedAt { get; set; }

        public bool IsDeleted { get; set; } = false;

        // Navigation property
        public virtual ICollection<AiMessage> Messages { get; set; } = new List<AiMessage>();
    }
}
