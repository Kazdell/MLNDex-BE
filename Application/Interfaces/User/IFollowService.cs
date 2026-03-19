using Application.DTOs.User;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces.User
{
    public interface IFollowService
    {
        Task<FollowResponseDto> FollowAsync(int userId, FollowRequestDto dto, CancellationToken ct = default);
        Task<bool> UnfollowAsync(int userId, int targetId, string targetType, CancellationToken ct = default);
        Task<List<FollowedSeriesDto>> GetFollowedSeriesAsync(int userId, CancellationToken ct = default);
        Task<FollowStatusDto> CheckFollowStatusAsync(int userId, int targetId, string targetType, CancellationToken ct = default);
        Task<int> GetFollowCountAsync(int targetId, string targetType, CancellationToken ct = default);
    }
}
