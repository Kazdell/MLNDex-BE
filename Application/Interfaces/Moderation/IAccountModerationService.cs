using Application.DTOs.Moderation;

namespace Application.Interfaces.Moderation
{
  public interface IAccountModerationService
  {
    Task<AccountActionResponse> ApplyAsync(
        int userId,
        int moderatorId,
        AccountActionRequest request,
        CancellationToken cancellationToken = default
    );
  }
}
