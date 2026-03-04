using System;

namespace Application.DTOs.Notification
{
    public class NotificationDto
    {
        public int NotificationId { get; set; }
        public string NotificationType { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string ActionUrl { get; set; } = null!;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
