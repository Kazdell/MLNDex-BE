using Application.DTOs.User;
using Application.Interfaces.Data;
using Application.Interfaces.User;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Services.User
{
    public class RatingService : IRatingService
    {
        private readonly IMlndexDbContext _db;

        public RatingService(IMlndexDbContext db)
        {
            _db = db;
        }

        public async Task<RatingResponseDto> UpsertRatingAsync(int userId, RatingRequestDto dto, CancellationToken ct = default)
        {
            if (dto.Score < 1 || dto.Score > 5)
                throw new ArgumentException("Score must be between 1 and 5.");

            // Check series exists
            var series = await _db.Series.FindAsync(new object[] { dto.SeriesId }, ct)
                ?? throw new ArgumentException("Series not found.");

            var existing = await _db.Ratings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.SeriesId == dto.SeriesId, ct);

            if (existing != null)
            {
                // Update
                existing.Score = dto.Score;
                existing.Review = dto.Review;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Create
                existing = new Rating
                {
                    UserId = userId,
                    SeriesId = dto.SeriesId,
                    Score = dto.Score,
                    Review = dto.Review,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.Ratings.Add(existing);
            }

            // Recalculate series average rating
            await _db.SaveChangesAsync(ct);
            await RecalculateSeriesRatingAsync(dto.SeriesId, ct);

            return new RatingResponseDto
            {
                RatingId = existing.RatingId,
                SeriesId = existing.SeriesId,
                Score = existing.Score,
                Review = existing.Review,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = existing.UpdatedAt
            };
        }

        public async Task<RatingResponseDto?> GetUserRatingAsync(int userId, int seriesId, CancellationToken ct = default)
        {
            var rating = await _db.Ratings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.SeriesId == seriesId, ct);

            if (rating == null) return null;

            return new RatingResponseDto
            {
                RatingId = rating.RatingId,
                SeriesId = rating.SeriesId,
                Score = rating.Score,
                Review = rating.Review,
                CreatedAt = rating.CreatedAt,
                UpdatedAt = rating.UpdatedAt
            };
        }

        public async Task<bool> DeleteRatingAsync(int userId, int seriesId, CancellationToken ct = default)
        {
            var rating = await _db.Ratings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.SeriesId == seriesId, ct);

            if (rating == null) return false;

            _db.Ratings.Remove(rating);
            await _db.SaveChangesAsync(ct);

            // Recalculate after delete
            await RecalculateSeriesRatingAsync(seriesId, ct);
            return true;
        }

        public async Task<SeriesRatingSummaryDto> GetSeriesRatingSummaryAsync(int seriesId, int? userId = null, CancellationToken ct = default)
        {
            var ratings = _db.Ratings.Where(r => r.SeriesId == seriesId);

            var totalRatings = await ratings.CountAsync(ct);
            var averageRating = totalRatings > 0
                ? await ratings.AverageAsync(r => (decimal)r.Score, ct)
                : 0m;

            if (averageRating > 5m) averageRating = 5m;

            int? userScore = null;
            if (userId.HasValue)
            {
                var userRating = await ratings.FirstOrDefaultAsync(r => r.UserId == userId.Value, ct);
                userScore = userRating?.Score;
            }

            return new SeriesRatingSummaryDto
            {
                AverageRating = Math.Round(averageRating, 1),
                TotalRatings = totalRatings,
                UserScore = userScore
            };
        }

        private async Task RecalculateSeriesRatingAsync(int seriesId, CancellationToken ct)
        {
            var series = await _db.Series.FindAsync(new object[] { seriesId }, ct);
            if (series == null) return;

            var ratings = _db.Ratings.Where(r => r.SeriesId == seriesId);
            var count = await ratings.CountAsync(ct);

            var avg = count > 0 ? await ratings.AverageAsync(r => (decimal)r.Score, ct) : 0m;
            if (avg > 5m) avg = 5m;

            series.TotalRatings = count;
            series.AverageRating = Math.Round(avg, 2);

            await _db.SaveChangesAsync(ct);
        }
    }
}
