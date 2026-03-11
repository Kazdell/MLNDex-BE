using Application.DTOs.Creator;
using Application.DTOs.Notification;
using Application.Interfaces.Data;
using Application.Interfaces.Notification;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Services.Notification
{
    public class NotificationService : INotificationService
    {
        private readonly IMlndexDbContext _db;
        private readonly INotificationPusher _pusher;

        public NotificationService(IMlndexDbContext db, INotificationPusher pusher)
        {
            _db = db;
            _pusher = pusher;
        }

        public async Task<PaginatedList<NotificationDto>> GetUserNotificationsAsync(int userId, int page = 1, int pageSize = 20)
        {
            var query = _db.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt);

            var totalCount = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return new PaginatedList<NotificationDto>
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                Items = items.Select(n => new NotificationDto
                {
                    NotificationId = n.NotificationId,
                    Title = n.Title,
                    Message = n.Message,
                    ActionUrl = n.ActionUrl,
                    IsRead = n.IsRead,
                    NotificationType = n.NotificationType.ToString(),
                    CreatedAt = n.CreatedAt
                }).ToList()
            };
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, int userId)
        {
            var notif = await _db.Notifications.FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);
            if (notif == null) return false;

            if (!notif.IsRead)
            {
                notif.IsRead = true;
                await _db.SaveChangesAsync();
            }
            return true;
        }

        public async Task<int> MarkAllAsReadAsync(int userId)
        {
            var unread = await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
            int count = unread.Count;
            if (count > 0)
            {
                foreach (var n in unread)
                {
                    n.IsRead = true;
                }
                await _db.SaveChangesAsync();
            }
            return count;
        }

        Task<PaginatedList<NotificationDto>> INotificationService.GetUserNotificationsAsync(int userId, int page, int pageSize)
        {
            throw new NotImplementedException();
        }

        public async Task<NotificationDto> CreateNotificationAsync(int userId, string title, string message, string actionUrl, NotificationType type)
        {
            var notif = new Domain.Entities.Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                ActionUrl = actionUrl,
                NotificationType = type,
                IsRead = false,
                CreatedAt = System.DateTime.UtcNow
            };
            
            _db.Notifications.Add(notif);
            await _db.SaveChangesAsync();
            
            var dto = new NotificationDto
            {
                NotificationId = notif.NotificationId,
                Title = notif.Title,
                Message = notif.Message,
                ActionUrl = notif.ActionUrl,
                IsRead = notif.IsRead,
                NotificationType = notif.NotificationType.ToString(),
                CreatedAt = notif.CreatedAt
            };
            
            await _pusher.PushNotificationAsync(userId, dto);
            
            return dto;
        }
    }
}
