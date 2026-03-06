using Application.DTOs.Chapter;
using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Application.Interfaces.AIModeration;

namespace Application.Services.Creator
{
  // Application/Services/Creator/ChapterService.cs
  public class ChapterService : IChapterService
  {
    private readonly IMlndexDbContext _db;
    private readonly IStorageService _storage;
    private readonly ILogger<ChapterService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public ChapterService(
        IMlndexDbContext db,
        IStorageService storage,
        ILogger<ChapterService> logger,
        IServiceScopeFactory scopeFactory)
    {
      _db = db;
      _storage = storage;
      _logger = logger;
      _scopeFactory = scopeFactory;
    }

    public async Task<CreateChapterResponseDto> CreateAsync(
        int creatorId,
        CreateChapterDto dto,
        CancellationToken cancellationToken = default)
    {
      // ── 1. Kiểm tra series tồn tại và thuộc về creator ───────────
      var series = await _db.Series
          .FirstOrDefaultAsync(
              s => s.SeriesId == dto.SeriesId && s.CreatorId == creatorId,
              cancellationToken)
          ?? throw new KeyNotFoundException(
              $"Series {dto.SeriesId} không tồn tại hoặc bạn không có quyền truy cập.");

      // ── 2. Kiểm tra trùng số chương ───────────────────────────────
      bool duplicate = await _db.Chapters.AnyAsync(
          c => c.SeriesId == dto.SeriesId && c.ChapterNumber == dto.ChapterNumber,
          cancellationToken);

      if (duplicate)
        throw new InvalidOperationException(
            $"Chương {dto.ChapterNumber} của truyện này đã tồn tại.");

      // ── 3. Upload ảnh trang lên Cloudinary ────────────────────────
      var uploadedUrls = new List<string>();
      var folder = $"chapters/{dto.SeriesId}";

      try
      {
        // ── 4. Build chapter entity ───────────────────────────────
        var chapter = new Chapter
        {
          SeriesId = dto.SeriesId,
          ChapterNumber = dto.ChapterNumber,
          Title = dto.Title,
          ContentType = ContentType.IMAGE,
          PageCount = dto.Pages?.Count ?? 0,
          Status = ChapterStatus.DRAFT,
          ModerationStatus = ModerationStatus.PENDING,
          PublishedAt = null,
        };

        _db.Chapters.Add(chapter);
        await _db.SaveChangesAsync(cancellationToken); // get ChapterId

        // ── 5. Upload pages và lưu ChapterPage ────────────────────
        if (dto.Pages != null && dto.Pages.Count > 0)
        {
          var pageFolder = $"chapters/{chapter.ChapterId}/pages";

          foreach (var (page, index) in dto.Pages.Select((p, i) => (p, i)))
          {
            var url = await _storage.UploadAsync(
                page.FileStream,
                page.FileName,
                pageFolder,
                cancellationToken);

            uploadedUrls.Add(url);

            _db.ChapterPages.Add(new ChapterPage
            {
              ChapterId = chapter.ChapterId,
              PageNumber = index + 1,
              ImageUrl = url,
            });
          }

          await _db.SaveChangesAsync(cancellationToken);

          // ── Gọi AI Moderation chạy ngầm ───────────────────────────
          _ = Task.Run(async () =>
          {
            try
            {
              using var scope = _scopeFactory.CreateScope();
              var moderationService = scope.ServiceProvider.GetRequiredService<IModerationService>();
              await moderationService.RunAiModerationAsync(chapter.ChapterId);
            }
            catch (Exception ex)
            {
              _logger.LogError(ex, "Lỗi khi chạy background AI kiểm duyệt cho Chapter {ChapterId}", chapter.ChapterId);
            }
          });
        }

        _logger.LogInformation(
            "Tạo chapter thành công. ChapterId: {ChapterId}, Chapter: {ChapterNumber}, SeriesId: {SeriesId}, Pages: {PageCount}.",
            chapter.ChapterId, chapter.ChapterNumber, dto.SeriesId, uploadedUrls.Count);

        return new CreateChapterResponseDto
        {
          ChapterId = chapter.ChapterId,
          SeriesId = chapter.SeriesId,
          ChapterNumber = chapter.ChapterNumber,
          Title = chapter.Title,
          PageCount = uploadedUrls.Count,
        };
      }
      catch (Exception ex)
      {
        // ── 6. Cleanup: Xóa ảnh đã upload nếu DB lỗi ─────────────
        if (uploadedUrls.Count > 0)
        {
          _logger.LogWarning(ex,
              "Lưu DB thất bại. Đang xóa {Count} ảnh đã upload.", uploadedUrls.Count);

          foreach (var url in uploadedUrls)
            await _storage.DeleteAsync(url, cancellationToken);
        }
        throw;
      }
    }

    public async Task<ChapterDetailDto?> GetChapterDetailAsync(
        int chapterId,
        CancellationToken cancellationToken = default)
    {
      var chapter = await _db.Chapters
          .Include(c => c.Series)
              .ThenInclude(s => s.Creator)
          .Include(c => c.Team)
          .Include(c => c.Pages.OrderBy(p => p.PageNumber))
          .FirstOrDefaultAsync(c => c.ChapterId == chapterId, cancellationToken);

      if (chapter == null) return null;

      var chapters = await _db.Chapters
          .Where(c => c.SeriesId == chapter.SeriesId)
          .OrderByDescending(c => c.ChapterNumber)
          .Select(c => new ChapterSummaryDto
          {
            ChapterId = c.ChapterId,
            ChapterNumber = c.ChapterNumber,
            Title = c.Title
          })
          .ToListAsync(cancellationToken);

      var prevChapterId = await _db.Chapters
          .Where(c => c.SeriesId == chapter.SeriesId && c.ChapterNumber < chapter.ChapterNumber)
          .OrderByDescending(c => c.ChapterNumber)
          .Select(c => (int?)c.ChapterId)
          .FirstOrDefaultAsync(cancellationToken);

      var nextChapterId = await _db.Chapters
          .Where(c => c.SeriesId == chapter.SeriesId && c.ChapterNumber > chapter.ChapterNumber)
          .OrderBy(c => c.ChapterNumber)
          .Select(c => (int?)c.ChapterId)
          .FirstOrDefaultAsync(cancellationToken);

      return new ChapterDetailDto
      {
        ChapterId = chapter.ChapterId,
        SeriesId = chapter.SeriesId,
        SeriesTitle = chapter.Series?.Title,
        UploaderName = chapter.Series?.Creator?.PenName,
        TranslatorTeamName = chapter.Team?.TeamName,
        ChapterNumber = chapter.ChapterNumber,
        Title = chapter.Title,
        PrevChapterId = prevChapterId,
        NextChapterId = nextChapterId,
        Chapters = chapters,
        Pages = chapter.Pages.Select(p => new ChapterPageResponseDto
        {
          PageId = p.PageId,
          ChapterId = p.ChapterId,
          PageNumber = p.PageNumber,
          ImageUrl = p.ImageUrl
        }).ToList()
      };
    }
  }
}
