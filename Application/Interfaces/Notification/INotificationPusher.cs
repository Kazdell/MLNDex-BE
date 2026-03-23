using Application.DTOs.Notification;
using System.Threading.Tasks;

namespace Application.Interfaces.Notification
{
  public interface INotificationPusher
  {
    Task PushNotificationAsync(int userId, NotificationDto dto);
  }
}
