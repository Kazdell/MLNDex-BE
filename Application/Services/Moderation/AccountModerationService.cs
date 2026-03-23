using Application.DTOs.Moderation;
using Application.Interfaces.Data;
using Application.Interfaces.Moderation;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Moderation
{
  public class AccountModerationService : IAccountModerationService
  {
    private readonly IMlndexDbContext _context;

    public AccountModerationService(IMlndexDbContext context)
    {
      _context = context;
    }

    public async Task<AccountActionResponse> ApplyAsync(
        int userId,
        int moderatorId,
        AccountActionRequest request,
        CancellationToken cancellationToken = default
    )
    {
      var user =
          await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
          ?? throw new KeyNotFoundException("User không tồn tại.");

      switch (request.Action)
      {
        case AccountActionType.WARN:
          await AddNotificationAsync(
              userId,
              "Cảnh báo tài khoản",
              request.Reason ?? "Vi phạm chính sách.",
              cancellationToken
          );
          break;
        case AccountActionType.DEACTIVATE:
          user.IsActive = false;
          await AddNotificationAsync(
              userId,
              "Tài khoản bị vô hiệu hóa",
              request.Reason ?? "Vi phạm chính sách.",
              cancellationToken
          );
          break;
        case AccountActionType.ACTIVATE:
          user.IsActive = true;
          await AddNotificationAsync(
              userId,
              "Tài khoản được kích hoạt lại",
              request.Reason ?? "",
              cancellationToken
          );
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }

      await _context.SaveChangesAsync(cancellationToken);

      return new AccountActionResponse
      {
        UserId = user.UserId,
        IsActive = user.IsActive,
        Message = request.Action switch
        {
          AccountActionType.WARN => "Đã gửi cảnh báo",
          AccountActionType.DEACTIVATE => "Đã vô hiệu hóa tài khoản",
          AccountActionType.ACTIVATE => "Đã kích hoạt tài khoản",
          _ => "",
        },
      };
    }

    private async Task AddNotificationAsync(
        int userId,
        string title,
        string message,
        CancellationToken cancellationToken
    )
    {
      _context.Notifications.Add(
          new Notification
          {
            UserId = userId,
            NotificationType = NotificationType.SYSTEM,
            Title = title,
            Message = message,
            ActionUrl = string.Empty,
            RelatedEntityId = null,
            RelatedEntityType = null,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
          }
      );
      await Task.CompletedTask;
    }
  }
}
