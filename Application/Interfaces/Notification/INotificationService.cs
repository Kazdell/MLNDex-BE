using Application.DTOs.Creator;
using Application.DTOs.Notification;
using System.Threading.Tasks;

namespace Application.Interfaces.Notification
{
    public interface INotificationService
    {
        Task<PaginatedList<NotificationDto>> GetUserNotificationsAsync(int userId, int page = 1, int pageSize = 20, bool? isRead = null);
        Task<bool> MarkAsReadAsync(int notificationId, int userId);
        Task<int> MarkAllAsReadAsync(int userId);
        Task<int> DeleteAllAsync(int userId);
        Task<int> CleanupOldNotificationsAsync(int daysOld = 7);
        Task<NotificationDto> CreateNotificationAsync(int userId, string title, string message, string actionUrl, Domain.Entities.NotificationType type);
    }
}
