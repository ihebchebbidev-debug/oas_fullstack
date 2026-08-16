using MyApi.Modules.AiChat.DTOs;

namespace MyApi.Modules.AiChat.Services
{
    public interface IAiChatService
    {
        // Conversation operations
        Task<AiConversationListResponseDto> GetConversationsAsync(string userId, int pageNumber = 1, int pageSize = 20, bool includeArchived = false);
        Task<AiConversationResponseDto?> GetConversationByIdAsync(int id, string userId, bool includeMessages = true);
        Task<AiConversationResponseDto> CreateConversationAsync(CreateConversationRequestDto dto, string userId);
        Task<AiConversationResponseDto?> UpdateConversationAsync(int id, UpdateConversationRequestDto dto, string userId);
        Task<bool> DeleteConversationAsync(int id, string userId);
        Task DeleteAllConversationsAsync(string userId);
        Task<bool> ArchiveConversationAsync(int id, string userId, bool archive = true);
        Task<bool> PinConversationAsync(int id, string userId, bool pin = true);

        // Message operations
        Task<AiMessageResponseDto> AddMessageAsync(AddMessageRequestDto dto, string userId);
        Task<List<AiMessageResponseDto>> AddMessagesAsync(BulkAddMessagesRequestDto dto, string userId);
        Task<AiMessageResponseDto?> UpdateMessageFeedbackAsync(int messageId, UpdateMessageFeedbackRequestDto dto, string userId);
        Task<bool> DeleteMessageAsync(int messageId, string userId);

        // Utility
        Task<string> GenerateConversationTitleAsync(string firstMessage);
    }
}
