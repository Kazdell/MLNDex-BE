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
  public class BookmarkService : IBookmarkService
  {
    private readonly IMlndexDbContext _db;

    public BookmarkService(IMlndexDbContext db)
    {
      _db = db;
    }

    public async Task<BookmarkResponseDto> UpsertBookmarkAsync(int userId, BookmarkRequestDto dto, CancellationToken ct = default)
    {
      // Upsert: if bookmark exists for this user+series, update it
      var existing = await _db.Bookmarks
          .FirstOrDefaultAsync(b => b.UserId == userId && b.SeriesId == dto.SeriesId, ct);

      if (existing != null)
      {
        existing.ChapterId = dto.ChapterId;
        existing.Note = dto.Note;
        existing.BookmarkedAt = DateTime.UtcNow;
      }
      else
      {
        existing = new Bookmark
        {
          UserId = userId,
          SeriesId = dto.SeriesId,
          ChapterId = dto.ChapterId,
          Note = dto.Note,
          BookmarkedAt = DateTime.UtcNow
        };
        _db.Bookmarks.Add(existing);
      }

      await _db.SaveChangesAsync(ct);

      return await MapToResponseDto(existing, ct);
    }

    public async Task<List<BookmarkResponseDto>> GetUserBookmarksAsync(int userId, CancellationToken ct = default)
    {
      var bookmarks = await _db.Bookmarks
          .Include(b => b.Series)
          .Include(b => b.Chapter)
          .Where(b => b.UserId == userId)
          .OrderByDescending(b => b.BookmarkedAt)
          .Select(b => new BookmarkResponseDto
          {
            BookmarkId = b.BookmarkId,
            SeriesId = b.SeriesId,
            SeriesTitle = b.Series.Title,
            CoverImageUrl = b.Series.CoverImageUrl,
            ChapterId = b.ChapterId,
            ChapterTitle = b.Chapter != null ? "Chương " + b.Chapter.ChapterNumber : null,
            Note = b.Note,
            BookmarkedAt = b.BookmarkedAt
          })
          .ToListAsync(ct);

      return bookmarks;
    }

    public async Task<BookmarkResponseDto?> GetBookmarkForSeriesAsync(int userId, int seriesId, CancellationToken ct = default)
    {
      var bookmark = await _db.Bookmarks
          .Include(b => b.Series)
          .Include(b => b.Chapter)
          .FirstOrDefaultAsync(b => b.UserId == userId && b.SeriesId == seriesId, ct);

      if (bookmark == null) return null;

      return new BookmarkResponseDto
      {
        BookmarkId = bookmark.BookmarkId,
        SeriesId = bookmark.SeriesId,
        SeriesTitle = bookmark.Series.Title,
        CoverImageUrl = bookmark.Series.CoverImageUrl,
        ChapterId = bookmark.ChapterId,
        ChapterTitle = bookmark.Chapter != null ? "Chương " + bookmark.Chapter.ChapterNumber : null,
        Note = bookmark.Note,
        BookmarkedAt = bookmark.BookmarkedAt
      };
    }

    public async Task<bool> DeleteBookmarkAsync(int userId, int bookmarkId, CancellationToken ct = default)
    {
      var bookmark = await _db.Bookmarks
          .FirstOrDefaultAsync(b => b.BookmarkId == bookmarkId && b.UserId == userId, ct);

      if (bookmark == null) return false;

      _db.Bookmarks.Remove(bookmark);
      await _db.SaveChangesAsync(ct);
      return true;
    }

    private async Task<BookmarkResponseDto> MapToResponseDto(Bookmark bookmark, CancellationToken ct)
    {
      var series = await _db.Series.FindAsync(new object[] { bookmark.SeriesId }, ct);
      Chapter? chapter = bookmark.ChapterId.HasValue
          ? await _db.Chapters.FindAsync(new object[] { bookmark.ChapterId.Value }, ct)
          : null;

      return new BookmarkResponseDto
      {
        BookmarkId = bookmark.BookmarkId,
        SeriesId = bookmark.SeriesId,
        SeriesTitle = series?.Title ?? "",
        CoverImageUrl = series?.CoverImageUrl,
        ChapterId = bookmark.ChapterId,
        ChapterTitle = chapter != null ? "Chương " + chapter.ChapterNumber : null,
        Note = bookmark.Note,
        BookmarkedAt = bookmark.BookmarkedAt
      };
    }
  }
}
