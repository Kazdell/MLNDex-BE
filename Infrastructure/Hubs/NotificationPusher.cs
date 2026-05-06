using Application.DTOs.Notification;
using Application.Interfaces.Notification;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace mlndex_backend.Hubs
{
  public class NotificationPusher : INotificationPusher
  {
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationPusher(IHubContext<NotificationHub> hubContext)
    {
      _hubContext = hubContext;
    }

    public async Task PushNotificationAsync(int userId, NotificationDto dto)
    {
      await _hubContext.Clients.Group($"User_{userId}").SendAsync("ReceiveNotification", dto);
    }

    public async Task PushReportResolvedEventAsync()
    {
      await _hubContext.Clients.All.SendAsync("ReportResolved");
    }
  }
}
