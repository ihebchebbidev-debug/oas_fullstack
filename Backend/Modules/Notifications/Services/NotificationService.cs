using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Modules.Notifications.DTOs;
using MyApi.Modules.Notifications.Models;

namespace MyApi.Modules.Notifications.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ApplicationDbContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<NotificationsResponseDto> GetUserNotificationsAsync(int userId, int page = 1, int limit = 50)
        {
            var query = _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt);

            var totalCount = await query.CountAsync();
            var unreadCount = await query.CountAsync(n => !n.IsRead);

            var notifications = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(n => MapToDto(n))
                .ToListAsync();

            return new NotificationsResponseDto
            {
                Notifications = notifications,
                UnreadCount = unreadCount,
                TotalCount = totalCount
            };
        }

        public async Task<NotificationDto?> GetNotificationByIdAsync(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            return notification == null ? null : MapToDto(notification);
        }

        public async Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto dto)
        {
            var notification = new Notification
            {
                UserId = dto.UserId,
                Title = dto.Title,
                Description = dto.Description,
                Type = dto.Type,
                Category = dto.Category,
                Link = dto.Link,
                RelatedEntityId = dto.RelatedEntityId,
                RelatedEntityType = dto.RelatedEntityType,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created notification {Id} for user {UserId}", notification.Id, notification.UserId);

            return MapToDto(notification);
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, int userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null) return false;

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> MarkMultipleAsReadAsync(List<int> notificationIds, int userId)
        {
            var notifications = await _context.Notifications
                .Where(n => notificationIds.Contains(n.Id) && n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (!notifications.Any()) return true;

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkAllAsReadAsync(int userId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (!notifications.Any()) return true;

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Marked all notifications as read for user {UserId}", userId);

            return true;
        }

        public async Task<bool> DeleteNotificationAsync(int id, int userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification == null) return false;

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        // Auto-generation methods for workflow events
        public async Task GenerateSaleNotificationAsync(int saleId, string saleNumber, string? contactName, int? assignedUserId = null)
        {
            var userIds = assignedUserId.HasValue
                ? new List<int> { assignedUserId.Value }
                : await GetAdminUserIdsAsync();

            var desc = $"Sale {saleNumber}{(contactName != null ? $" for {contactName}" : "")} has been created.";
            await BulkCreateNotificationsAsync(userIds.Select(uid => new CreateNotificationDto
            {
                UserId = uid,
                Title = "New Sale Created",
                Description = desc,
                Type = "success",
                Category = "sale",
                Link = $"/dashboard/sales/{saleId}",
                RelatedEntityId = saleId,
                RelatedEntityType = "Sale"
            }));
        }

        public async Task GenerateOfferNotificationAsync(int offerId, string offerNumber, string? contactName, int? assignedUserId = null)
        {
            var userIds = assignedUserId.HasValue
                ? new List<int> { assignedUserId.Value }
                : await GetAdminUserIdsAsync();

            var desc = $"Offer {offerNumber}{(contactName != null ? $" for {contactName}" : "")} has been created.";
            await BulkCreateNotificationsAsync(userIds.Select(uid => new CreateNotificationDto
            {
                UserId = uid,
                Title = "New Offer Created",
                Description = desc,
                Type = "info",
                Category = "offer",
                Link = $"/dashboard/offers/{offerId}",
                RelatedEntityId = offerId,
                RelatedEntityType = "Offer"
            }));
        }

        public async Task GenerateServiceOrderNotificationAsync(int serviceOrderId, string serviceOrderNumber, string? contactName, int? assignedUserId = null)
        {
            var userIds = assignedUserId.HasValue
                ? new List<int> { assignedUserId.Value }
                : await GetAdminUserIdsAsync();

            var desc = $"Service Order {serviceOrderNumber}{(contactName != null ? $" for {contactName}" : "")} has been created.";
            await BulkCreateNotificationsAsync(userIds.Select(uid => new CreateNotificationDto
            {
                UserId = uid,
                Title = "New Service Order",
                Description = desc,
                Type = "info",
                Category = "service_order",
                Link = $"/dashboard/field/service-orders/{serviceOrderId}",
                RelatedEntityId = serviceOrderId,
                RelatedEntityType = "ServiceOrder"
            }));
        }

        public async Task GenerateTaskDueNotificationAsync(int taskId, string taskTitle, int userId)
        {
            await CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = userId,
                Title = "Task Due Today",
                Description = $"\"{taskTitle}\" is due today.",
                Type = "warning",
                Category = "task",
                Link = "/dashboard/tasks",
                RelatedEntityId = taskId,
                RelatedEntityType = "Task"
            });
        }

        public async Task GenerateTaskOverdueNotificationAsync(int taskId, string taskTitle, int userId)
        {
            await CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = userId,
                Title = "Task Overdue",
                Description = $"\"{taskTitle}\" is overdue and needs attention.",
                Type = "warning",
                Category = "task",
                Link = "/dashboard/tasks",
                RelatedEntityId = taskId,
                RelatedEntityType = "Task"
            });
        }

        public async Task GenerateTaskAssignedNotificationAsync(int taskId, string taskTitle, int assignedUserId, string assignedByName, int? projectId = null)
        {
            var link = projectId.HasValue 
                ? $"/dashboard/tasks/projects/{projectId}" 
                : "/dashboard/tasks";
            
            await CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = assignedUserId,
                Title = "Task Assigned to You",
                Description = $"{assignedByName} assigned you the task \"{taskTitle}\".",
                Type = "info",
                Category = "task",
                Link = link,
                RelatedEntityId = taskId,
                RelatedEntityType = "Task"
            });
        }

        private async Task BulkCreateNotificationsAsync(IEnumerable<CreateNotificationDto> dtos)
        {
            var notifications = dtos.Select(dto => new Notification
            {
                UserId = dto.UserId,
                Title = dto.Title,
                Description = dto.Description,
                Type = dto.Type,
                Category = dto.Category,
                Link = dto.Link,
                RelatedEntityId = dto.RelatedEntityId,
                RelatedEntityType = dto.RelatedEntityType,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            if (notifications.Count == 0) return;

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }

        private async Task<List<int>> GetAdminUserIdsAsync()
        {
            return await _context.MainAdminUsers
                .Select(u => u.Id)
                .ToListAsync();
        }

        private static NotificationDto MapToDto(Notification n) => new()
        {
            Id = n.Id,
            UserId = n.UserId,
            Title = n.Title,
            Description = n.Description,
            Type = n.Type,
            Category = n.Category,
            Link = n.Link,
            RelatedEntityId = n.RelatedEntityId,
            RelatedEntityType = n.RelatedEntityType,
            IsRead = n.IsRead,
            ReadAt = n.ReadAt,
            CreatedAt = n.CreatedAt
        };
    }
}
