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
using Application.Interfaces.Notification;

namespace Application.Services.Creator
{
  // Application/Services/Creator/ChapterService.cs
  public class ChapterService : IChapterService
  {
    private readonly IMlndexDbContext _db;
    private readonly IStorageService _storage;
    private readonly ILogger<ChapterService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INotificationService _notificationService;

    public ChapterService(
        IMlndexDbContext db,
        IStorageService storage,
        ILogger<ChapterService> logger,
        IServiceScopeFactory scopeFactory,
        INotificationService notificationService)
    {
      _db = db;
      _storage = storage;
      _logger = logger;
      _scopeFactory = scopeFactory;
      _notificationService = notificationService;
    }

    public async Task<CreateChapterResponseDto> CreateAsync(
        int userId,
        CreateChapterDto dto,
        CancellationToken cancellationToken = default)
    {
      // ── 1. Kiểm tra quyền tải lên (Tác giả hoặc Nhóm dịch) ───────────
      Series? series = null;
      if (dto.TeamId == null)
      {
          series = await _db.Series.FirstOrDefaultAsync(s => s.SeriesId == dto.SeriesId && s.Creator.UserId == userId, cancellationToken);
          if (series == null) throw new KeyNotFoundException($"Series {dto.SeriesId} không tồn tại hoặc bạn không phải là tác giả.");
      }
      else
      {
          series = await _db.Series
              .Include(s => s.Creator)
              .FirstOrDefaultAsync(s => s.SeriesId == dto.SeriesId, cancellationToken);
          if (series == null) throw new KeyNotFoundException($"Series {dto.SeriesId} không tồn tại.");

          var isAuthorizedMember = await _db.TeamMembers.AnyAsync(m => 
              m.TeamId == dto.TeamId && 
              m.UserId == userId && 
              m.IsActive &&
              (m.Role == TeamMemberRole.LEADER || m.Role == TeamMemberRole.EDITOR || m.Role == TeamMemberRole.TRANSLATOR),
              cancellationToken);

          if (!isAuthorizedMember) throw new UnauthorizedAccessException("Bạn không có quyền tải chương lên danh nghĩa nhóm dịch này.");

          var hasPermission = await _db.TranslationPermissions.AnyAsync(p => 
              p.TeamId == dto.TeamId && 
              p.SeriesId == dto.SeriesId && 
              p.Status == TranslationPermissionStatus.GRANTED,
              cancellationToken);

          if (!hasPermission) throw new InvalidOperationException("Nhóm dịch của bạn chưa được cấp phép hoặc đã bị thu hồi quyền dịch bộ truyện này.");
      }

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
          TeamId = dto.TeamId,
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

          // Send notification to author if uploaded by team
          if (dto.TeamId != null && series?.Creator != null)
          {
              var team = await _db.TranslationTeams.FindAsync(dto.TeamId);
              if (team != null)
              {
                  await _notificationService.CreateNotificationAsync(
                      series.Creator.UserId,
                      team.TeamName,
                      $"Đã đăng chương {chapter.ChapterNumber} cho bộ {series.Title}",
                      $"/series/{series.SeriesId}/chapters/{chapter.ChapterId}",
                      NotificationType.NEW_CHAPTER
                  );
              }
          }

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
