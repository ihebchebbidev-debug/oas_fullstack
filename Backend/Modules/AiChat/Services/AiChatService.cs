using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.AiChat.DTOs;
using MyApi.Modules.AiChat.Models;

namespace MyApi.Modules.AiChat.Services
{
    public class AiChatService : IAiChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AiChatService> _logger;

        public AiChatService(ApplicationDbContext context, ILogger<AiChatService> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Conversation Operations

        public async Task<AiConversationListResponseDto> GetConversationsAsync(
            string userId, 
            int pageNumber = 1, 
            int pageSize = 20, 
            bool includeArchived = false)
        {
            var query = _context.AiConversations
                .Where(c => c.UserId == userId && !c.IsDeleted);

            if (!includeArchived)
            {
                query = query.Where(c => !c.IsArchived);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var conversations = await query
                .OrderByDescending(c => c.IsPinned)
                .ThenByDescending(c => c.LastMessageAt ?? c.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(c => MapToResponseDto(c, false))
                .ToListAsync();

            return new AiConversationListResponseDto
            {
                Conversations = conversations,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }

        public async Task<AiConversationResponseDto?> GetConversationByIdAsync(
            int id, 
            string userId, 
            bool includeMessages = true)
        {
            var query = _context.AiConversations
                .Where(c => c.Id == id && c.UserId == userId && !c.IsDeleted);

            if (includeMessages)
            {
                query = query.Include(c => c.Messages.OrderBy(m => m.CreatedAt));
            }

            var conversation = await query.FirstOrDefaultAsync();
            
            return conversation != null ? MapToResponseDto(conversation, includeMessages) : null;
        }

        public async Task<AiConversationResponseDto> CreateConversationAsync(
            CreateConversationRequestDto dto, 
            string userId)
        {
            var conversation = new AiConversation
            {
                UserId = userId,
                Title = dto.Title ?? "New Conversation",
                CreatedAt = DateTime.UtcNow
            };

            _context.AiConversations.Add(conversation);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created conversation {ConversationId} for user {UserId}", conversation.Id, userId);

            return MapToResponseDto(conversation, false);
        }

        public async Task<AiConversationResponseDto?> UpdateConversationAsync(
            int id, 
            UpdateConversationRequestDto dto, 
            string userId)
        {
            var conversation = await _context.AiConversations
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && !c.IsDeleted);

            if (conversation == null) return null;

            if (dto.Title != null) conversation.Title = dto.Title;
            if (dto.IsArchived.HasValue) conversation.IsArchived = dto.IsArchived.Value;
            if (dto.IsPinned.HasValue) conversation.IsPinned = dto.IsPinned.Value;
            
            conversation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return MapToResponseDto(conversation, false);
        }

        public async Task<bool> DeleteConversationAsync(int id, string userId)
        {
            var conversation = await _context.AiConversations
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && !c.IsDeleted);

            if (conversation == null) return false;

            conversation.IsDeleted = true;
            conversation.DeletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted conversation {ConversationId} for user {UserId}", id, userId);

            return true;
        }

        public async Task<bool> ArchiveConversationAsync(int id, string userId, bool archive = true)
        {
            var conversation = await _context.AiConversations
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && !c.IsDeleted);

            if (conversation == null) return false;

            conversation.IsArchived = archive;
            conversation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> PinConversationAsync(int id, string userId, bool pin = true)
        {
            var conversation = await _context.AiConversations
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && !c.IsDeleted);

            if (conversation == null) return false;

            conversation.IsPinned = pin;
            conversation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task DeleteAllConversationsAsync(string userId)
        {
            var conversations = await _context.AiConversations
                .Where(c => c.UserId == userId && !c.IsDeleted)
                .ToListAsync();

            foreach (var conversation in conversations)
            {
                conversation.IsDeleted = true;
                conversation.DeletedAt = DateTime.UtcNow;
                conversation.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Deleted {Count} conversations for user {UserId}", conversations.Count, userId);
        }

        #endregion

        #region Message Operations

        public async Task<AiMessageResponseDto> AddMessageAsync(AddMessageRequestDto dto, string userId)
        {
            // Verify conversation belongs to user
            var conversation = await _context.AiConversations
                .FirstOrDefaultAsync(c => c.Id == dto.ConversationId && c.UserId == userId && !c.IsDeleted);

            if (conversation == null)
            {
                throw new InvalidOperationException("Conversation not found or access denied");
            }

            var message = new AiMessage
            {
                ConversationId = dto.ConversationId,
                Role = dto.Role,
                Content = dto.Content,
                Metadata = dto.Metadata,
                CreatedAt = DateTime.UtcNow
            };

            _context.AiMessages.Add(message);

            // Update conversation metadata
            conversation.LastMessageAt = message.CreatedAt;
            conversation.MessageCount++;
            conversation.UpdatedAt = DateTime.UtcNow;

            // Auto-generate title from first user message if still default
            if (dto.Role == "user" && conversation.MessageCount == 1 && 
                (conversation.Title == "New Conversation" || string.IsNullOrEmpty(conversation.Title)))
            {
                conversation.Title = await GenerateConversationTitleAsync(dto.Content);
            }

            await _context.SaveChangesAsync();

            return MapMessageToResponseDto(message);
        }

        public async Task<List<AiMessageResponseDto>> AddMessagesAsync(BulkAddMessagesRequestDto dto, string userId)
        {
            // Verify conversation belongs to user
            var conversation = await _context.AiConversations
                .FirstOrDefaultAsync(c => c.Id == dto.ConversationId && c.UserId == userId && !c.IsDeleted);

            if (conversation == null)
            {
                throw new InvalidOperationException("Conversation not found or access denied");
            }

            var now = DateTime.UtcNow;
            var messages = new List<AiMessage>();

            foreach (var msgDto in dto.Messages)
            {
                var message = new AiMessage
                {
                    ConversationId = dto.ConversationId,
                    Role = msgDto.Role,
                    Content = msgDto.Content,
                    CreatedAt = now
                };
                messages.Add(message);
            }

            _context.AiMessages.AddRange(messages);

            // Update conversation metadata
            conversation.LastMessageAt = now;
            conversation.MessageCount += messages.Count;
            conversation.UpdatedAt = now;

            // Auto-generate title from first user message if still default
            var firstUserMessage = dto.Messages.FirstOrDefault(m => m.Role == "user");
            if (firstUserMessage != null && conversation.MessageCount == messages.Count && 
                (conversation.Title == "New Conversation" || string.IsNullOrEmpty(conversation.Title)))
            {
                conversation.Title = await GenerateConversationTitleAsync(firstUserMessage.Content);
            }

            await _context.SaveChangesAsync();

            return messages.Select(MapMessageToResponseDto).ToList();
        }

        public async Task<AiMessageResponseDto?> UpdateMessageFeedbackAsync(
            int messageId, 
            UpdateMessageFeedbackRequestDto dto, 
            string userId)
        {
            var message = await _context.AiMessages
                .Include(m => m.Conversation)
                .FirstOrDefaultAsync(m => m.Id == messageId && m.Conversation!.UserId == userId);

            if (message == null) return null;

            message.Feedback = dto.Feedback;

            await _context.SaveChangesAsync();

            return MapMessageToResponseDto(message);
        }

        public async Task<bool> DeleteMessageAsync(int messageId, string userId)
        {
            var message = await _context.AiMessages
                .Include(m => m.Conversation)
                .FirstOrDefaultAsync(m => m.Id == messageId && m.Conversation!.UserId == userId);

            if (message == null) return false;

            _context.AiMessages.Remove(message);

            // Update conversation message count
            if (message.Conversation != null)
            {
                message.Conversation.MessageCount--;
            }

            await _context.SaveChangesAsync();

            return true;
        }

        #endregion

        #region Utility Methods

        public Task<string> GenerateConversationTitleAsync(string firstMessage)
        {
            // Simple title generation: take first 50 chars of the message
            var title = firstMessage.Length > 50 
                ? firstMessage.Substring(0, 47) + "..." 
                : firstMessage;

            // Remove newlines and extra spaces
            title = System.Text.RegularExpressions.Regex.Replace(title, @"\s+", " ").Trim();

            return Task.FromResult(title);
        }

        private static AiConversationResponseDto MapToResponseDto(AiConversation conversation, bool includeMessages)
        {
            return new AiConversationResponseDto
            {
                Id = conversation.Id,
                UserId = conversation.UserId,
                Title = conversation.Title,
                Summary = conversation.Summary,
                LastMessageAt = conversation.LastMessageAt,
                MessageCount = conversation.MessageCount,
                IsArchived = conversation.IsArchived,
                IsPinned = conversation.IsPinned,
                CreatedAt = conversation.CreatedAt,
                UpdatedAt = conversation.UpdatedAt,
                Messages = includeMessages 
                    ? conversation.Messages.Select(MapMessageToResponseDto).ToList() 
                    : null
            };
        }

        private static AiMessageResponseDto MapMessageToResponseDto(AiMessage message)
        {
            return new AiMessageResponseDto
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                Role = message.Role,
                Content = message.Content,
                Feedback = message.Feedback,
                IsRegenerated = message.IsRegenerated,
                CreatedAt = message.CreatedAt
            };
        }

        #endregion
    }
}
