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
      var history = await _db.ReadingHistories
          .FirstOrDefaultAsync(h => h.UserId == userId && h.SeriesId == dto.SeriesId, cancellationToken);

      if (history == null)
      {
        history = new ReadingHistory
        {
          UserId = userId,
          SeriesId = dto.SeriesId,
          LastChapterId = dto.ChapterId,
          LastPageNumber = dto.PageNumber,
          LastReadAt = DateTime.UtcNow
        };
        _db.ReadingHistories.Add(history);
      }
      else
      {
        history.LastChapterId = dto.ChapterId;
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
  }
}
