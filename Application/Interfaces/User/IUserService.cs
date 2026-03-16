using Application.DTOs.User;
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
        Task<List<UserSearchDto>> SearchUsersAsync(string query, CancellationToken cancellationToken);
        Task<UserProfileDto?> GetPublicProfileAsync(string username, CancellationToken cancellationToken);
    }
}
