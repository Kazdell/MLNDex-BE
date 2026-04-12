using Application.DTOs.User;
using Application.Interfaces.Data;
using Application.Interfaces.User;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Services.User
{
  public class FollowService : IFollowService
  {
    private readonly IMlndexDbContext _db;

    public FollowService(IMlndexDbContext db)
    {
      _db = db;
    }

    public async Task<FollowResponseDto> FollowAsync(int userId, FollowRequestDto dto, CancellationToken ct = default)
    {
      if (!Enum.TryParse<FollowTargetType>(dto.TargetType, true, out var targetType))
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.INVALID_INPUT);

      // Check if already following
      var existing = await _db.Follows
          .FirstOrDefaultAsync(f => f.UserId == userId && f.TargetId == dto.TargetId && f.TargetType == targetType, ct);

      if (existing != null)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

      var follow = new Follow
      {
        UserId = userId,
        TargetId = dto.TargetId,
        TargetType = targetType,
        FollowedAt = DateTime.UtcNow
      };

      _db.Follows.Add(follow);
      await _db.SaveChangesAsync(ct);

      return new FollowResponseDto
      {
        FollowId = follow.FollowId,
        TargetId = follow.TargetId,
        TargetType = follow.TargetType.ToString(),
        FollowedAt = follow.FollowedAt
      };
    }

    public async Task<bool> UnfollowAsync(int userId, int targetId, string targetType, CancellationToken ct = default)
    {
      if (!Enum.TryParse<FollowTargetType>(targetType, true, out var type))
        return false;

      var follow = await _db.Follows
          .FirstOrDefaultAsync(f => f.UserId == userId && f.TargetId == targetId && f.TargetType == type, ct);

      if (follow == null) return false;

      _db.Follows.Remove(follow);
      await _db.SaveChangesAsync(ct);
      return true;
    }

    public async Task<List<FollowedSeriesDto>> GetFollowedSeriesAsync(int userId, CancellationToken ct = default)
    {
      var follows = await _db.Follows
          .Where(f => f.UserId == userId && f.TargetType == FollowTargetType.SERIES)
          .OrderByDescending(f => f.FollowedAt)
          .ToListAsync(ct);

      var seriesIds = follows.Select(f => f.TargetId).ToList();

      var seriesList = await _db.Series
          .Include(s => s.Creator)
              .ThenInclude(c => c.User)
          .Include(s => s.Chapters)
          .Where(s => seriesIds.Contains(s.SeriesId))
          .ToDictionaryAsync(s => s.SeriesId, ct);

      return follows
          .Where(f => seriesList.ContainsKey(f.TargetId))
          .Select(f =>
          {
            var s = seriesList[f.TargetId];
            return new FollowedSeriesDto
            {
              FollowId = f.FollowId,
              SeriesId = s.SeriesId,
              Title = s.Title,
              CoverImageUrl = s.CoverImageUrl,
              Status = s.Status.ToString(),
              AverageRating = s.AverageRating,
              FollowedAt = f.FollowedAt,
              CreatorName = s.Creator?.User?.Username,
              LatestChapter = s.Chapters?.OrderByDescending(c => c.CreatedAt).FirstOrDefault()?.ChapterNumber.ToString() ?? "?"
            };
          })
          .ToList();
    }

    public async Task<FollowStatusDto> CheckFollowStatusAsync(int userId, int targetId, string targetType, CancellationToken ct = default)
    {
      if (!Enum.TryParse<FollowTargetType>(targetType, true, out var type))
        return new FollowStatusDto { IsFollowing = false };

      var follow = await _db.Follows
          .FirstOrDefaultAsync(f => f.UserId == userId && f.TargetId == targetId && f.TargetType == type, ct);

      return new FollowStatusDto
      {
        IsFollowing = follow != null,
        FollowId = follow?.FollowId
      };
    }

    public async Task<int> GetFollowCountAsync(int targetId, string targetType, CancellationToken ct = default)
    {
      if (!Enum.TryParse<FollowTargetType>(targetType, true, out var type))
        return 0;

      return await _db.Follows
          .CountAsync(f => f.TargetId == targetId && f.TargetType == type, ct);
    }
  }
}
