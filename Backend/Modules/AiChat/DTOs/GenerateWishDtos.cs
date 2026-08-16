using System.ComponentModel.DataAnnotations;

namespace MyApi.Modules.AiChat.DTOs
{
    // ── Chat message (OpenAI-compatible format) ──
    public class ChatMessageDto
    {
        [Required]
        public string Role { get; set; } = "user"; // "system" | "user" | "assistant"

        [Required]
        [MinLength(1)]
        public string Content { get; set; } = string.Empty;
    }

    // ── Request ──
    public class GenerateWishRequestDto
    {
        /// <summary>
        /// Simple prompt (legacy). If Messages[] is provided, this is ignored.
        /// </summary>
        [MaxLength(4000, ErrorMessage = "Prompt cannot exceed 4000 characters")]
        public string? Prompt { get; set; }

        /// <summary>
        /// Chat messages array (preferred). Supports system/user/assistant roles.
        /// </summary>
        public List<ChatMessageDto>? Messages { get; set; }

        /// <summary>
        /// Ollama model name. Defaults to "llama3:8b".
        /// </summary>
        [MaxLength(100)]
        public string? Model { get; set; }

        /// <summary>
        /// Optional conversation ID to persist the exchange.
        /// </summary>
        public int? ConversationId { get; set; }

        /// <summary>
        /// Temperature for generation (0.0 - 1.0). Defaults to 0.7.
        /// </summary>
        public float? Temperature { get; set; }

        /// <summary>
        /// Max tokens to generate.
        /// </summary>
        public int? MaxTokens { get; set; }

        /// <summary>
        /// Whether to stream the response via SSE.
        /// </summary>
        public bool Stream { get; set; } = false;
    }

    // ── Response ──
    public class GenerateWishResponseDto
    {
        public bool Success { get; set; }
        public string Response { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public long TotalDurationNs { get; set; }
        public int? ConversationId { get; set; }
        public string? Error { get; set; }
    }

    // ── Ollama /api/generate (legacy single-prompt) ──
    internal class OllamaGenerateRequest
    {
        public string Model { get; set; } = "mistral";
        public string Prompt { get; set; } = string.Empty;
        public bool Stream { get; set; } = false;
    }

    internal class OllamaGenerateResponse
    {
        public string Model { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public bool Done { get; set; }
        public long Total_duration { get; set; }
    }

    // ── Ollama /api/chat (multi-turn conversation) ──
    internal class OllamaChatMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }

    internal class OllamaChatRequest
    {
        public string Model { get; set; } = "mistral";
        public List<OllamaChatMessage> Messages { get; set; } = new();
        public bool Stream { get; set; } = false;
        public OllamaChatOptions? Options { get; set; }
    }

    internal class OllamaChatOptions
    {
        public float? Temperature { get; set; }
        public int? Num_predict { get; set; } // max tokens
    }

    internal class OllamaChatResponse
    {
        public string Model { get; set; } = string.Empty;
        public OllamaChatMessage? Message { get; set; }
        public bool Done { get; set; }
        public long Total_duration { get; set; }
    }

    // ── Ollama streaming chunk (for /api/chat with stream=true) ──
    internal class OllamaChatStreamChunk
    {
        public string Model { get; set; } = string.Empty;
        public OllamaChatMessage? Message { get; set; }
        public bool Done { get; set; }
        public long Total_duration { get; set; }
    }
}
