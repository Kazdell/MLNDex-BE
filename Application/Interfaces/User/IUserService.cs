using Application.DTOs.User;
using Application.DTOs.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces.User
{
  public interface IUserService
  {
    Task<UserProfileDto?> GetProfileAsync(int userId, CancellationToken cancellationToken);
    Task<bool> UpdateProfileAsync(int userId, UpdateProfileDto dto, CancellationToken cancellationToken);
    Task<List<ReadingHistoryDto>> GetReadingHistoryAsync(int userId, CancellationToken cancellationToken);
    Task<List<VipPlanDto>> GetVipPlansAsync(CancellationToken cancellationToken);
    Task<PagedResult<UserSearchDto>> SearchUsersAsync(string query, int page, int pageSize, string? roleFilter, string? statusFilter, CancellationToken cancellationToken);
    Task<UserProfileDto?> GetPublicProfileAsync(string username, CancellationToken cancellationToken);
    Task<UserSettingsDto?> GetUserSettingsAsync(int userId, CancellationToken cancellationToken);
    Task<bool> UpdateUserSettingsAsync(int userId, UserSettingsDto dto, CancellationToken cancellationToken);
    Task<UserStatsDto> GetUserStatsAsync(int days, CancellationToken cancellationToken);
  }
}
