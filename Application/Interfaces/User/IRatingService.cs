using Application.DTOs.User;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces.User
{
    public interface IRatingService
    {
        Task<RatingResponseDto> UpsertRatingAsync(int userId, RatingRequestDto dto, CancellationToken ct = default);
        Task<RatingResponseDto?> GetUserRatingAsync(int userId, int seriesId, CancellationToken ct = default);
        Task<bool> DeleteRatingAsync(int userId, int seriesId, CancellationToken ct = default);
        Task<SeriesRatingSummaryDto> GetSeriesRatingSummaryAsync(int seriesId, int? userId = null, CancellationToken ct = default);
    }
}
