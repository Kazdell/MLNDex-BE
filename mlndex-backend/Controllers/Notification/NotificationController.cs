using Application.Interfaces.Notification;
using Microsoft.AspNetCore.Mvc;
using mlndex_backend.Controllers;
using System.Threading.Tasks;

namespace mlndex_backend.Controllers.Notification
{
    [ApiController]
    [Route("api/notifications")]
    public class NotificationController : BaseController
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            // TODO: Replace with explicit ID extraction from Context JWT
            int currentUserId = 1;
            var result = await _notificationService.GetUserNotificationsAsync(currentUserId, page, pageSize);
            return OkResponse(result);
        }

        [HttpPatch("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            int currentUserId = 1;
            var isSuccess = await _notificationService.MarkAsReadAsync(id, currentUserId);
            
            if (!isSuccess)
                return NotFoundResponse("Notification not found or unauthorized.");

            return OkResponse("Marked as read.");
        }

        [HttpPatch("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            int currentUserId = 1;
            int count = await _notificationService.MarkAllAsReadAsync(currentUserId);
            return OkResponse($"Marked {count} notifications as read.");
        }
    }
}
