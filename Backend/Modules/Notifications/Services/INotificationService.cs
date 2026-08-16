using MyApi.Modules.Notifications.DTOs;

namespace MyApi.Modules.Notifications.Services
{
    public interface INotificationService
    {
        Task<NotificationsResponseDto> GetUserNotificationsAsync(int userId, int page = 1, int limit = 50);
        Task<NotificationDto?> GetNotificationByIdAsync(int id);
        Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto dto);
        Task<bool> MarkAsReadAsync(int notificationId, int userId);
        Task<bool> MarkMultipleAsReadAsync(List<int> notificationIds, int userId);
        Task<bool> MarkAllAsReadAsync(int userId);
        Task<bool> DeleteNotificationAsync(int id, int userId);
        Task<int> GetUnreadCountAsync(int userId);
        
        // Auto-generation methods
        Task GenerateSaleNotificationAsync(int saleId, string saleNumber, string? contactName, int? assignedUserId = null);
        Task GenerateOfferNotificationAsync(int offerId, string offerNumber, string? contactName, int? assignedUserId = null);
        Task GenerateServiceOrderNotificationAsync(int serviceOrderId, string serviceOrderNumber, string? contactName, int? assignedUserId = null);
        Task GenerateTaskDueNotificationAsync(int taskId, string taskTitle, int userId);
        Task GenerateTaskOverdueNotificationAsync(int taskId, string taskTitle, int userId);
        Task GenerateTaskAssignedNotificationAsync(int taskId, string taskTitle, int assignedUserId, string assignedByName, int? projectId = null);
    }
}
