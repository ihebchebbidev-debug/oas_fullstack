using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.AiChat.DTOs
{
    // Response DTOs
    public class AiConversationResponseDto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public int MessageCount { get; set; }
        public bool IsArchived { get; set; }
        public bool IsPinned { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<AiMessageResponseDto>? Messages { get; set; }
    }

    public class AiMessageResponseDto
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? Feedback { get; set; }
        public bool IsRegenerated { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AiConversationListResponseDto
    {
        public List<AiConversationResponseDto> Conversations { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    // Request DTOs
    public class CreateConversationRequestDto
    {
        [MaxLength(255)]
        public string? Title { get; set; }
    }

    public class UpdateConversationRequestDto
    {
        [MaxLength(255)]
        public string? Title { get; set; }
        public bool? IsArchived { get; set; }
        public bool? IsPinned { get; set; }
    }

    public class AddMessageRequestDto
    {
        [Required]
        public int ConversationId { get; set; }

        [Required]
        [RegularExpression("^(user|assistant|system)$")]
        public string Role { get; set; } = "user";

        [Required]
        public string Content { get; set; } = string.Empty;

        public string? Metadata { get; set; }
    }

    public class UpdateMessageFeedbackRequestDto
    {
        [RegularExpression("^(liked|disliked)$")]
        public string? Feedback { get; set; }
    }

    public class BulkAddMessagesRequestDto
    {
        [Required]
        public int ConversationId { get; set; }

        [Required]
        public List<MessageToAddDto> Messages { get; set; } = new();
    }

    public class MessageToAddDto
    {
        [Required]
        [RegularExpression("^(user|assistant|system)$")]
        public string Role { get; set; } = "user";

        [Required]
        public string Content { get; set; } = string.Empty;
    }
}
