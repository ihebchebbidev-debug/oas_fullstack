using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.Calendar.Models
{
    [ModuleScope("calendar")]
    [Table("event_types")]
    public class EventType : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [MaxLength(50)]
        public string Id { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        [MaxLength(7)]
        public string Color { get; set; } = "#3B82F6";

        [Column("is_default")]
        public bool IsDefault { get; set; } = false;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<CalendarEvent> CalendarEvents { get; set; } = new List<CalendarEvent>();
    }
}