using System;
using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.Dispatches.DTOs
{
    public class CreateNoteDto
    {
        [Required]
        public string Content { get; set; } = null!;
        public string? Category { get; set; }
        public string? Priority { get; set; }
    }

    public class NoteDto
    {
        public int Id { get; set; }
        public int DispatchId { get; set; }
        public string Content { get; set; } = null!;
        public string? Category { get; set; }
        public string? Priority { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
