using System;

namespace MyApi.Modules.Dispatches.DTOs
{
    public class DispatchQueryParams
    {
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public string? TechnicianId { get; set; }
        public int? ServiceOrderId { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? SearchTerm { get; set; }
        public int PageSize { get; set; } = 20;
        public int PageNumber { get; set; } = 1;
        public string? SortBy { get; set; }
        public string? SortOrder { get; set; }
    }
}
