using MyApi.Modules.AiChat.DTOs;

namespace MyApi.Modules.AiChat.Services
{
    public interface IOllamaService
    {
        /// <summary>
        /// Sends a prompt or chat messages to the local Ollama LLM and returns the response.
        /// </summary>
        Task<GenerateWishResponseDto> GenerateAsync(GenerateWishRequestDto request, string userId);

        /// <summary>
        /// Streams a chat response via SSE. Writes chunks to the response stream.
        /// </summary>
        Task StreamChatAsync(GenerateWishRequestDto request, string userId, Stream responseStream, CancellationToken cancellationToken);
    }
}
