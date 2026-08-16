using System;
using Microsoft.AspNetCore.Http;

namespace MyApi.Modules.Dispatches.DTOs
{
    public class AttachmentUploadResponseDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = null!;
        public string? FileType { get; set; }
        public long FileSizeBytes { get; set; }
        public string? Category { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
