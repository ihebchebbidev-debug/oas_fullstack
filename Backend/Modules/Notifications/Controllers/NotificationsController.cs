using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApi.Modules.Notifications.DTOs;
using MyApi.Modules.Notifications.Services;
using System.Security.Claims;

namespace MyApi.Modules.Notifications.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(INotificationService notificationService, ILogger<NotificationsController> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst("userId")?.Value;
            
            if (int.TryParse(userIdClaim, out var userId))
                return userId;
            
            return 0;
        }

        /// <summary>
        /// Get notifications for the current user
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<NotificationsResponseDto>> GetNotifications(
            [FromQuery] int page = 1, 
            [FromQuery] int limit = 50)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized("User not authenticated");

            var result = await _notificationService.GetUserNotificationsAsync(userId, page, limit);
            return Ok(result);
        }

        /// <summary>
        /// Get notification by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<NotificationDto>> GetNotification(int id)
        {
            var notification = await _notificationService.GetNotificationByIdAsync(id);
            if (notification == null)
                return NotFound();

            var userId = GetCurrentUserId();
            if (notification.UserId != userId)
                return Forbid();

            return Ok(notification);
        }

        /// <summary>
        /// Get unread count for current user
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<ActionResult<int>> GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized("User not authenticated");

            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(new { unreadCount = count });
        }

        /// <summary>
        /// Create a new notification (admin/system use)
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<NotificationDto>> CreateNotification([FromBody] CreateNotificationDto dto)
        {
            var notification = await _notificationService.CreateNotificationAsync(dto);
            return CreatedAtAction(nameof(GetNotification), new { id = notification.Id }, notification);
        }

        /// <summary>
        /// Mark a single notification as read
        /// </summary>
        [HttpPatch("{id}/read")]
        public async Task<ActionResult> MarkAsRead(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized("User not authenticated");

            var success = await _notificationService.MarkAsReadAsync(id, userId);
            if (!success)
                return NotFound();

            return Ok(new { success = true });
        }

        /// <summary>
        /// Mark multiple notifications as read
        /// </summary>
        [HttpPatch("read")]
        public async Task<ActionResult> MarkMultipleAsRead([FromBody] MarkNotificationsReadDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized("User not authenticated");

            await _notificationService.MarkMultipleAsReadAsync(dto.NotificationIds, userId);
            return Ok(new { success = true });
        }

        /// <summary>
        /// Mark all notifications as read for current user
        /// </summary>
        [HttpPatch("read-all")]
        public async Task<ActionResult> MarkAllAsRead()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized("User not authenticated");

            await _notificationService.MarkAllAsReadAsync(userId);
            return Ok(new { success = true });
        }

        /// <summary>
        /// Delete a notification
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteNotification(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized("User not authenticated");

            var success = await _notificationService.DeleteNotificationAsync(id, userId);
            if (!success)
                return NotFound();

            return NoContent();
        }
    }
}
