using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApi.Infrastructure;

namespace MyApi.Modules.ExternalEndpoints.Models
{
    [Table("ExternalEndpointLogs")]
    public class ExternalEndpointLog : ITenantEntity
    {
        public int TenantId { get; set; }

        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("EndpointId")]
        public int EndpointId { get; set; }

        [Column("Method")]
        [MaxLength(10)]
        public string Method { get; set; } = "POST";

        [Column("Headers")]
        public string? Headers { get; set; }

        [Column("QueryString")]
        public string? QueryString { get; set; }

        [Column("Body")]
        public string? Body { get; set; }

        [Column("SourceIp")]
        [MaxLength(50)]
        public string? SourceIp { get; set; }

        [Column("StatusCode")]
        public int StatusCode { get; set; } = 200;

        [Column("ResponseBody")]
        public string? ResponseBody { get; set; }

        [Required]
        [Column("ReceivedAt")]
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        [Column("ProcessedAt")]
        public DateTime? ProcessedAt { get; set; }

        [Column("IsRead")]
        public bool IsRead { get; set; } = false;

        /// <summary>MIME type of the inbound request body (e.g. "application/json", "application/xml", "application/x-www-form-urlencoded").</summary>
        [Column("ContentType")]
        [MaxLength(100)]
        public string? ContentType { get; set; }

        /// <summary>Company / tenant identifier supplied by the sender via the X-Company-ID header or ?company_id= query param.
        /// Useful for multi-tenant ERP setups where one Flowentra endpoint handles traffic from multiple companies.</summary>
        [Column("CompanyId")]
        [MaxLength(100)]
        public string? CompanyId { get; set; }

        // Navigation
        [ForeignKey("EndpointId")]
        public virtual ExternalEndpoint? Endpoint { get; set; }
    }
}
