using Application.DTOs.Notification;
using Application.DTOs.Series;
using System.Threading.Tasks;

namespace Application.Interfaces.Notification
{
    public interface INotificationService
    {
        Task<PaginatedList<NotificationDto>> GetUserNotificationsAsync(int userId, int page = 1, int pageSize = 20);
        Task<bool> MarkAsReadAsync(int notificationId, int userId);
        Task<int> MarkAllAsReadAsync(int userId);
    }
}
