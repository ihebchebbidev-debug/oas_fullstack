using Microsoft.AspNetCore.Http;

namespace MyApi.Modules.Dispatches.DTOs
{
    public class AttachmentUploadDto
    {
        public IFormFile? File { get; set; }

        public string? Category { get; set; }

        public string? Description { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }
    }
}
