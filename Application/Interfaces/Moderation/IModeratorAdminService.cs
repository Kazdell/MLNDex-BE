using Application.DTOs.Moderation;

namespace Application.Interfaces.Moderation
{
  public interface IModeratorAdminService
  {
    Task<ModeratorListResponse> GetModeratorsAsync(
        ModeratorListRequest request,
        CancellationToken cancellationToken = default
    );
    Task<ModeratorDto> AssignAsync(int userId, CancellationToken cancellationToken = default);
    Task RemoveAsync(int userId, CancellationToken cancellationToken = default);
  }
}
