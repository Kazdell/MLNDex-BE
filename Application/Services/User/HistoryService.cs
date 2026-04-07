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
  public class HistoryService : IHistoryService
  {
    private readonly IMlndexDbContext _db;

    public HistoryService(IMlndexDbContext db)
    {
      _db = db;
    }

    public async Task<bool> UpdateHistoryAsync(int userId, ReadingHistoryUpdateDto dto, CancellationToken cancellationToken = default)
    {
      // ── Resolve chapterId: could be a real ChapterId or a TranslationId ──
      var resolvedChapterId = dto.ChapterId;
      var chapterExists = await _db.Chapters.AnyAsync(c => c.ChapterId == dto.ChapterId, cancellationToken);
      if (!chapterExists)
      {
        // Fallback: check if it's a TranslationId and resolve to real ChapterId
        var translation = await _db.Translations
            .Where(t => t.TranslationId == dto.ChapterId)
            .Select(t => t.ChapterId)
            .FirstOrDefaultAsync(cancellationToken);

        if (translation > 0)
          resolvedChapterId = translation;
        else
          return false; // Neither a valid Chapter nor Translation
      }

      var history = await _db.ReadingHistories
          .FirstOrDefaultAsync(h => h.UserId == userId && h.SeriesId == dto.SeriesId, cancellationToken);

      if (history == null)
      {
        history = new ReadingHistory
        {
          UserId = userId,
          SeriesId = dto.SeriesId,
          LastChapterId = resolvedChapterId,
          LastPageNumber = dto.PageNumber,
          LastReadAt = DateTime.UtcNow
        };
        _db.ReadingHistories.Add(history);
      }
      else
      {
        history.LastChapterId = resolvedChapterId;
        history.LastPageNumber = dto.PageNumber;
        history.LastReadAt = DateTime.UtcNow;
      }

      await _db.SaveChangesAsync(cancellationToken);
      return true;
    }

    public async Task<List<ReadingHistoryResponseDto>> GetUserHistoryAsync(int userId, CancellationToken cancellationToken = default)
    {
      return await _db.ReadingHistories
          .Include(h => h.Series)
          .Include(h => h.LastChapter)
          .Where(h => h.UserId == userId)
          .OrderByDescending(h => h.LastReadAt)
          .Select(h => new ReadingHistoryResponseDto
          {
            HistoryId = h.HistoryId,
            SeriesId = h.SeriesId,
            Title = h.Series.Title,
            CoverUrl = h.Series.CoverImageUrl,
            LastChapterId = h.LastChapterId,
            LastChapterTitle = "Chương " + h.LastChapter.ChapterNumber,
            LastPageNumber = h.LastPageNumber,
            Progress = 100, // Tạm thời để 100%
            LastReadAt = h.LastReadAt
          })
          .ToListAsync(cancellationToken);
    }

    public async Task<bool> RemoveFromHistoryAsync(int userId, int seriesId, CancellationToken cancellationToken = default)
    {
      var history = await _db.ReadingHistories
          .FirstOrDefaultAsync(h => h.UserId == userId && h.SeriesId == seriesId, cancellationToken);

      if (history != null)
      {
        _db.ReadingHistories.Remove(history);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
      }
      return false;
    }

    public async Task<bool> ClearAllHistoryAsync(int userId, CancellationToken cancellationToken = default)
    {
      var histories = await _db.ReadingHistories
          .Where(h => h.UserId == userId)
          .ToListAsync(cancellationToken);

      if (histories.Any())
      {
        _db.ReadingHistories.RemoveRange(histories);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
      }
      return false;
    }

    public async Task<ReadingStatsDto> GetReadingStatsAsync(int userId, CancellationToken cancellationToken = default)
    {
      var totalSeriesRead = await _db.ReadingHistories
          .CountAsync(h => h.UserId == userId, cancellationToken);

      var totalChaptersRead = totalSeriesRead; // Approximation: 1 history entry = at least 1 chapter read

      var totalFollowing = await _db.Follows
          .CountAsync(f => f.UserId == userId, cancellationToken);

      var totalRated = await _db.Ratings
          .CountAsync(r => r.UserId == userId, cancellationToken);

      var totalBookmarks = await _db.Bookmarks
          .CountAsync(b => b.UserId == userId, cancellationToken);

      var lastActive = await _db.ReadingHistories
          .Where(h => h.UserId == userId)
          .OrderByDescending(h => h.LastReadAt)
          .Select(h => (DateTime?)h.LastReadAt)
          .FirstOrDefaultAsync(cancellationToken);

      // Top Genres (last read)
      var seriesIds = await _db.ReadingHistories
          .Where(h => h.UserId == userId)
          .Select(h => h.SeriesId)
          .ToListAsync(cancellationToken);

      var genresQuery = await _db.SeriesGenres
          .Include(sg => sg.Genre)
          .Where(sg => seriesIds.Contains(sg.SeriesId))
          .GroupBy(sg => sg.Genre.Name)
          .Select(g => new GenreStatDto
          {
            Genre = g.Key,
            Count = g.Count()
          })
          .OrderByDescending(g => g.Count)
          .Take(8)
          .ToListAsync(cancellationToken);

      // Monthly Activity (Last 6 months)
      var monthlyActivity = new List<MonthlyActivityDto>();
      var now = DateTime.UtcNow;
      var sixMonthsAgo = now.AddMonths(-5);
      sixMonthsAgo = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1, 0, 0, 0, DateTimeKind.Utc);

      var recentHistory = await _db.ReadingHistories
          .Where(h => h.UserId == userId && h.LastReadAt >= sixMonthsAgo)
          .Select(h => h.LastReadAt)
          .ToListAsync(cancellationToken);

      for (int i = 5; i >= 0; i--)
      {
        var d = now.AddMonths(-i);
        var label = "T" + d.Month;
        var count = recentHistory.Count(h => h.Year == d.Year && h.Month == d.Month);

        monthlyActivity.Add(new MonthlyActivityDto
        {
          Label = label,
          Count = count
        });
      }

      // Recent Activity
      var recentActivityQuery = await _db.ReadingHistories
          .Include(h => h.Series)
          .Include(h => h.LastChapter)
          .Where(h => h.UserId == userId)
          .OrderByDescending(h => h.LastReadAt)
          .Take(10)
          .Select(h => new ReadingHistoryResponseDto
          {
            HistoryId = h.HistoryId,
            SeriesId = h.SeriesId,
            Title = h.Series.Title,
            CoverUrl = h.Series.CoverImageUrl,
            LastChapterId = h.LastChapterId,
            LastChapterTitle = "Chương " + h.LastChapter.ChapterNumber,
            LastPageNumber = h.LastPageNumber,
            Progress = 100, // Tạm thời để 100%
            LastReadAt = h.LastReadAt
          })
          .ToListAsync(cancellationToken);

      return new ReadingStatsDto
      {
        TotalSeriesRead = totalSeriesRead,
        TotalChaptersRead = totalChaptersRead,
        TotalFollowing = totalFollowing,
        TotalRated = totalRated,
        TotalBookmarks = totalBookmarks,
        LastActiveAt = lastActive,
        TopGenres = genresQuery,
        MonthlyActivity = monthlyActivity,
        RecentActivity = recentActivityQuery
      };
    }
  }
}
