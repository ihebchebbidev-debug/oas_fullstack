using System.Text.Json.Serialization;

namespace MyApi.Modules.Notifications.DTOs
{
    public class NotificationDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("userId")]
        public int UserId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "info";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "system";

        [JsonPropertyName("link")]
        public string? Link { get; set; }

        [JsonPropertyName("relatedEntityId")]
        public int? RelatedEntityId { get; set; }

        [JsonPropertyName("relatedEntityType")]
        public string? RelatedEntityType { get; set; }

        [JsonPropertyName("isRead")]
        public bool IsRead { get; set; }

        [JsonPropertyName("readAt")]
        public DateTime? ReadAt { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }

    public class CreateNotificationDto
    {
        [JsonPropertyName("userId")]
        public int UserId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "info";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "system";

        [JsonPropertyName("link")]
        public string? Link { get; set; }

        [JsonPropertyName("relatedEntityId")]
        public int? RelatedEntityId { get; set; }

        [JsonPropertyName("relatedEntityType")]
        public string? RelatedEntityType { get; set; }
    }

    public class MarkNotificationsReadDto
    {
        [JsonPropertyName("notificationIds")]
        public List<int> NotificationIds { get; set; } = new();
    }

    public class NotificationsResponseDto
    {
        [JsonPropertyName("notifications")]
        public List<NotificationDto> Notifications { get; set; } = new();

        [JsonPropertyName("unreadCount")]
        public int UnreadCount { get; set; }

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }
    }
}
