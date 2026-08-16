using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.EmailAccounts.Models
{
    public class EmailBlocklistItem : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ConnectedEmailAccountId { get; set; }

        [ForeignKey(nameof(ConnectedEmailAccountId))]
        public ConnectedEmailAccount? ConnectedEmailAccount { get; set; }

        [Required]
        [MaxLength(255)]
        public string Handle { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}