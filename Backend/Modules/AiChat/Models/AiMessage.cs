using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.AiChat.Models
{
    [Table("AiMessages")]
    public class AiMessage : ITenantEntity
    {
        public int TenantId { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int ConversationId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = "user"; // user, assistant, system

        [Required]
        public string Content { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Feedback { get; set; } // liked, disliked, null

        public bool IsRegenerated { get; set; } = false;

        [Column(TypeName = "jsonb")]
        public string? Metadata { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey("ConversationId")]
        public virtual AiConversation? Conversation { get; set; }
    }
}
