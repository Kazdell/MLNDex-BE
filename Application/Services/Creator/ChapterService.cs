using Application.DTOs.AIModeration;
using Application.DTOs.Chapter;
using Application.Interfaces.AIModeration;
using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Application.Interfaces.Notification;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services.Creator
{
  // Application/Services/Creator/ChapterService.cs
  public class ChapterService : IChapterService
  {
    private readonly IMlndexDbContext _db;
    private readonly IStorageService _storage;
    private readonly ILogger<ChapterService> _logger;
    private readonly INotificationService _notificationService;
    private readonly IModerationService _moderationService;

    public ChapterService(
        IMlndexDbContext db,
        IStorageService storage,
        ILogger<ChapterService> logger,
        INotificationService notificationService,
        IModerationService moderationService)
    {
      _db = db;
      _storage = storage;
      _logger = logger;
      _notificationService = notificationService;
      _moderationService = moderationService;
    }

    public async Task<CreateChapterResponseDto> CreateAsync(
    int userId,
    CreateChapterDto dto,
    CancellationToken cancellationToken = default)
    {
      // ── 1. Kiểm tra quyền tải lên (Tác giả hoặc Nhóm dịch) ───────────
      if (dto.TeamId != null)
        throw new InvalidOperationException("Vui lòng sử dụng API dành riêng cho nhóm dịch để đăng bản dịch.");

      var series = await _db.Series.FirstOrDefaultAsync(s => s.SeriesId == dto.SeriesId && s.Creator.UserId == userId, cancellationToken);
      if (series == null) throw new KeyNotFoundException($"Series {dto.SeriesId} không tồn tại hoặc bạn không phải là tác giả.");

      // ── 2. Rate Limit: Max 10 chapters/ngày ─────────────────────────
      var todayUtc = DateTime.UtcNow.Date;
      var chaptersToday = await _db.Chapters
          .Where(c => c.Series.Creator.UserId == userId && c.CreatedAt >= todayUtc)
          .CountAsync(cancellationToken);
      if (chaptersToday >= 10)
        throw new InvalidOperationException(
            "Bạn đã đạt giới hạn 10 chapter/ngày. Vui lòng quay lại ngày mai.");

      // ── 2b. Cooldown: 15 phút giữa mỗi lần đăng ───────────────────
      // var cutoff = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
      // var lastChapter = await _db.Chapters
      //     .Where(c => c.Series.Creator.UserId == userId && c.CreatedAt > cutoff)
      //     .OrderByDescending(c => c.CreatedAt)
      //     .Select(c => c.CreatedAt)
      //     .FirstOrDefaultAsync(cancellationToken);
      // if (lastChapter > cutoff)
      // {
      //     var elapsed = DateTime.UtcNow - lastChapter;
      //     if (elapsed.TotalMinutes < 15)
      //     {
      //         var remaining = TimeSpan.FromMinutes(15) - elapsed;
      //         throw new InvalidOperationException(
      //             $"Vui lòng đợi {remaining.Minutes} phút {remaining.Seconds} giây nữa trước khi đăng chapter mới.");
      //     }
      // }

      // ── 3. Queue Flood Protection: Max 10 pending jobs ─────────────────
      var pendingJobs = await _db.ModerationQueues
          .Where(q => q.Status == QueueStatus.PENDING
              && q.ContentType == ModerationQueueContentType.CHAPTER
              && _db.Chapters.Any(c => c.ChapterId == q.ContentId
                  && c.Series.Creator.UserId == userId))
          .CountAsync(cancellationToken);
      if (pendingJobs >= 10)
        throw new InvalidOperationException(
            "Bạn đang có 10 chapter chờ kiểm duyệt. Hãy đợi kết quả trước khi tải thêm.");

      // ── 4. Kiểm tra trùng số chương (chương gốc) ──
      bool duplicate = await _db.Chapters.AnyAsync(
          c => c.SeriesId == dto.SeriesId
               && c.ChapterNumber == dto.ChapterNumber
               && c.TeamId == null,
          cancellationToken);

      if (duplicate)
        throw new InvalidOperationException($"Chương {dto.ChapterNumber} đã tồn tại.");

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
          CreatedAt = DateTime.UtcNow,
        };

        _db.Chapters.Add(chapter);
        await _db.SaveChangesAsync(cancellationToken); // get ChapterId

        // ── 5. Upload pages và lưu ChapterPage ────────────────────
        if (dto.Pages != null && dto.Pages.Count > 0)
        {
          var pageFolder = $"chapters/{chapter.ChapterId}/pages";

          // Upload song song, tối đa 4 ảnh cùng lúc
          var semaphore = new SemaphoreSlim(4);

          var uploadTasks = dto.Pages.Select(async (page, index) =>
          {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
              var url = await _storage.UploadAsync(
                            page.FileStream,
                            page.FileName,
                            pageFolder,
                            CancellationToken.None);
              return (url, index);
            }
            finally
            {
              semaphore.Release();
            }
          });

          var results = await Task.WhenAll(uploadTasks);

          foreach (var (url, index) in results.OrderBy(r => r.index))
          {
            uploadedUrls.Add(url);
            _db.ChapterPages.Add(new ChapterPage
            {
              ChapterId = chapter.ChapterId,
              PageNumber = index + 1,
              ImageUrl = url,
            });
          }

          await _db.SaveChangesAsync(cancellationToken);

          // Original chapter: No notification sent to author since author uploaded it.

          // ── Gọi AI Moderation chạy ngầm ───────────────────────────
          await _moderationService.EnqueueChapterForModerationAsync(
              chapter.ChapterId, cancellationToken);
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
          .Include(c => c.Team)
          .Include(c => c.Language)
          .Where(c => c.SeriesId == chapter.SeriesId && c.Status == ChapterStatus.PUBLISHED)
          .OrderByDescending(c => c.ChapterNumber)
          .Select(c => new ChapterSummaryDto
          {
            ChapterId = c.ChapterId,
            ChapterNumber = c.ChapterNumber,
            Title = c.Title,
            TeamId = c.TeamId,
            TeamName = c.Team != null ? c.Team.TeamName : null,
            LanguageCode = c.Language != null ? c.Language.Code : null,
            LanguageName = c.Language != null ? c.Language.Name : null,
            IsOriginal = c.TeamId == null
          })
          .ToListAsync(cancellationToken);

      var prevChapterId = await _db.Chapters
          .Where(c => c.SeriesId == chapter.SeriesId && c.ChapterNumber < chapter.ChapterNumber && c.TeamId == chapter.TeamId && c.Status == ChapterStatus.PUBLISHED)
          .OrderByDescending(c => c.ChapterNumber)
          .Select(c => (int?)c.ChapterId)
          .FirstOrDefaultAsync(cancellationToken);

      var nextChapterId = await _db.Chapters
          .Where(c => c.SeriesId == chapter.SeriesId && c.ChapterNumber > chapter.ChapterNumber && c.TeamId == chapter.TeamId && c.Status == ChapterStatus.PUBLISHED)
          .OrderBy(c => c.ChapterNumber)
          .Select(c => (int?)c.ChapterId)
          .FirstOrDefaultAsync(cancellationToken);

      var dto = new ChapterDetailDto
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

      // Translation Ecosystem logic:
      if (chapter.TeamId != null)
      {
        var translation = await _db.Translations
            .Include(t => t.TranslationCredits)
                .ThenInclude(tc => tc.User)
            .Include(t => t.TeamJoins)
                .ThenInclude(tj => tj.Team)
            .FirstOrDefaultAsync(t => t.ChapterId == chapter.ChapterId, cancellationToken);

        if (translation != null)
        {
          dto.IsTranslation = true;
          dto.IsOfficial = translation.IsOfficial;
          dto.IsOutdated = translation.IsOutdated;
          dto.IsOrphan = translation.IsOrphan;

          if (translation.TranslationCredits != null)
          {
            dto.TranslationCredits = translation.TranslationCredits.Select(tc => new TranslationCreditDetailDto
            {
              UserId = tc.UserId,
              Username = tc.User.Username,
              Role = tc.Role.ToString()
            }).ToList();
          }

          if (translation.TeamJoins != null)
          {
            dto.JointTeams = translation.TeamJoins.Select(tj => new JointTeamDetailDto
            {
              TeamId = tj.TeamId,
              TeamName = tj.Team.TeamName
            }).ToList();
          }
        }
      }

      return dto;
    }

    public async Task<ChapterModerationStatusDto> GetModerationStatusAsync(
int chapterId, CancellationToken ct = default)
    {
      var job = await _db.ModerationQueues
          .Where(q => q.ContentId == chapterId
              && q.ContentType == ModerationQueueContentType.CHAPTER)
          .OrderByDescending(q => q.FlaggedAt)
          .FirstOrDefaultAsync(ct);

      // ── Fallback: chapter đã có kết quả AI rồi (APPROVED/REJECTED) nhưng queue missing/stuck ──
      if (job == null || (job.Status != QueueStatus.RESOLVED && job.Status != QueueStatus.IN_REVIEW))
      {
        var chapter = await _db.Chapters.FirstOrDefaultAsync(c => c.ChapterId == chapterId, ct);
        if (chapter != null &&
            (chapter.ModerationStatus == ModerationStatus.APPROVED || chapter.ModerationStatus == ModerationStatus.REJECTED))
        {
          // Return completed even if AiScoresJson is empty (e.g. old chapters)
          AiModerationResultDto? result = null;
          if (!string.IsNullOrEmpty(chapter.AiScoresJson))
            result = await _moderationService.GetResultAsync(chapterId, ct);

          return new ChapterModerationStatusDto
          {
            ChapterId = chapterId,
            Status = "completed",
            Flagged = result?.Flagged ?? (chapter.ModerationStatus == ModerationStatus.REJECTED),
            FlaggedReason = result?.FlaggedReason,
            CategoryScores = result?.CategoryScores ?? new Dictionary<string, double>(),
            PerPageResults = result?.PerPageResults,
          };
        }

        // No job AND no AI result → truly pending
        if (job == null)
          return new ChapterModerationStatusDto
          {
            ChapterId = chapterId,
            Status = "pending"
          };
      }

      // Tính queue position nếu đang pending
      int? queuePos = null;
      if (job.Status == QueueStatus.PENDING)
      {
        queuePos = await _db.ModerationQueues
            .Where(q => q.Status == QueueStatus.PENDING
                && (q.Priority == QueuePriority.HIGH && job.Priority != QueuePriority.HIGH
                    || q.FlaggedAt < job.FlaggedAt))
            .CountAsync(ct) + 1;
      }

      var status = job.Status switch
      {
        QueueStatus.PENDING => "pending",
        QueueStatus.IN_REVIEW => "processing",
        QueueStatus.RESOLVED => "completed",
        QueueStatus.DISMISSED => "failed",
        _ => "pending"
      };

      // Lấy kết quả AI nếu đã xong
      AiModerationResultDto? result2 = null;
      if (job.Status == QueueStatus.RESOLVED)
        result2 = await _moderationService.GetResultAsync(chapterId, ct);

      return new ChapterModerationStatusDto
      {
        ChapterId = chapterId,
        Status = status,
        QueuePos = queuePos,
        Flagged = result2?.Flagged,
        FlaggedReason = result2?.FlaggedReason,
        CategoryScores = result2?.CategoryScores,
        PerPageResults = result2?.PerPageResults,
      };
    }

    public async Task RetryModerationAsync(int chapterId, CancellationToken ct = default)
    {
      var chapter = await _db.Chapters
          .FirstOrDefaultAsync(c => c.ChapterId == chapterId, ct)
          ?? throw new KeyNotFoundException($"Không tìm thấy chapter {chapterId}.");

      // Block retry if chapter is currently being processed
      // But allow if queue is PENDING but chapter already has a result (stale entry from old code)
      var activeJob = await _db.ModerationQueues
          .Where(q => q.ContentId == chapterId
              && q.ContentType == ModerationQueueContentType.CHAPTER
              && (q.Status == QueueStatus.PENDING || q.Status == QueueStatus.IN_REVIEW))
          .FirstOrDefaultAsync(ct);

      if (activeJob != null)
      {
        // Stale entry: queue stuck at PENDING but chapter already moderated → allow retry
        var isStale = chapter.ModerationStatus == ModerationStatus.APPROVED
                   || chapter.ModerationStatus == ModerationStatus.REJECTED;
        if (!isStale)
          throw new InvalidOperationException("Chapter đang trong hàng đợi hoặc đang được xử lý. Vui lòng đợi.");
      }

      // ── Retry Cooldown: 2 phút giữa 2 lần retry ──────────────────
      var lastJob = await _db.ModerationQueues
          .Where(q => q.ContentId == chapterId
              && q.ContentType == ModerationQueueContentType.CHAPTER)
          .OrderByDescending(q => q.FlaggedAt)
          .FirstOrDefaultAsync(ct);

      if (lastJob?.LastRetryAt.HasValue == true
          && (DateTime.UtcNow - lastJob.LastRetryAt.Value).TotalMinutes < 2)
      {
        var remaining = 2 - (DateTime.UtcNow - lastJob.LastRetryAt.Value).TotalMinutes;
        throw new InvalidOperationException(
            $"Vui lòng đợi {Math.Ceiling(remaining)} phút trước khi thử lại.");
      }

      // Reset chapter status
      chapter.ModerationStatus = ModerationStatus.PENDING;
      chapter.Status = ChapterStatus.DRAFT;
      await _db.SaveChangesAsync(ct);

      // EnqueueChapterForModerationAsync handles: cleanup old entries → create new PENDING → signal worker
      await _moderationService.EnqueueChapterForModerationAsync(chapterId, ct);

      _logger.LogInformation(
          "[ChapterService] ChapterId={ChapterId} đã được retry vào moderation queue.",
          chapterId);
    }

    public async Task<List<ChapterListItemDto>> GetBySeriesAsync(int seriesId, int userId, CancellationToken ct = default)
    {
      // Verify ownership
      var series = await _db.Series
          .Include(s => s.Creator)
          .FirstOrDefaultAsync(s => s.SeriesId == seriesId, ct)
          ?? throw new KeyNotFoundException("Series không tồn tại.");

      if (series.Creator.UserId != userId)
        throw new UnauthorizedAccessException("Bạn không có quyền xem chapters của series này.");

      return await _db.Chapters
          .Where(c => c.SeriesId == seriesId)
          .OrderByDescending(c => c.ChapterNumber)
          .Select(c => new ChapterListItemDto
          {
            ChapterId = c.ChapterId,
            ChapterNumber = c.ChapterNumber,
            Title = c.Title,
            Status = c.Status.ToString(),
            ModerationStatus = c.ModerationStatus.ToString(),
            PageCount = c.PageCount ?? 0,
            Views = c.Views,
            PublishedAt = c.PublishedAt,
            CreatedAt = c.CreatedAt,
          })
          .ToListAsync(ct);
    }

    // ── GET FOR EDIT (với ownership check + moderation info) ─────────────
    public async Task<ChapterDetailDto?> GetForEditAsync(
        int chapterId, int userId, CancellationToken ct = default)
    {
      var chapter = await _db.Chapters
          .Include(c => c.Series)
              .ThenInclude(s => s.Creator)
          .Include(c => c.Team)
          .Include(c => c.Pages.OrderBy(p => p.PageNumber))
          .Include(c => c.Language)
          .FirstOrDefaultAsync(c => c.ChapterId == chapterId, ct);

      if (chapter == null) return null;

      // Ownership check
      if (chapter.Series?.Creator?.UserId != userId)
        throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa chương này.");

      // Lấy moderation reason từ AI Report nếu bị reject
      string? moderationReason = null;
      if (chapter.ModerationStatus == ModerationStatus.REJECTED)
      {
        var aiReport = await _db.Reports
            .Where(r => r.ContentId == chapterId
                     && r.ContentType == ReportTargetType.ChapterTranslation
                     && r.Description != null
                     && r.Description.StartsWith("AI_"))
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => r.Description)
            .FirstOrDefaultAsync(ct);

        if (aiReport != null)
        {
          moderationReason = aiReport.Replace("AI_", "").Split(" (Score:")[0].Trim();
        }
      }

      return new ChapterDetailDto
      {
        ChapterId = chapter.ChapterId,
        SeriesId = chapter.SeriesId,
        SeriesTitle = chapter.Series?.Title,
        UploaderName = chapter.Series?.Creator?.PenName,
        TranslatorTeamName = chapter.Team?.TeamName,
        ChapterNumber = chapter.ChapterNumber,
        Title = chapter.Title,
        ModerationStatus = chapter.ModerationStatus.ToString(),
        Language = chapter.Language?.Name,
        ModerationReason = moderationReason,
        Pages = chapter.Pages.Select(p => new ChapterPageResponseDto
        {
          PageId = p.PageId,
          ChapterId = p.ChapterId,
          PageNumber = p.PageNumber,
          ImageUrl = p.ImageUrl
        }).ToList()
      };
    }

    // ── UPDATE CHAPTER ──────────────────────────────────────────────────
    public async Task<CreateChapterResponseDto> UpdateAsync(
        int chapterId, int userId, UpdateChapterDto dto,
        List<UploadPageDto>? newPages, CancellationToken ct = default)
    {
      var chapter = await _db.Chapters
          .Include(c => c.Series)
              .ThenInclude(s => s.Creator)
          .Include(c => c.Pages)
          .Include(c => c.Translations)
          .FirstOrDefaultAsync(c => c.ChapterId == chapterId, ct)
          ?? throw new KeyNotFoundException($"Không tìm thấy chương {chapterId}.");

      // Ownership check
      if (chapter.Series?.Creator?.UserId != userId)
        throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa chương này.");

      // Update metadata
      chapter.ChapterNumber = dto.ChapterNumber;
      chapter.Title = dto.Title;
      if (dto.LanguageId.HasValue)
        chapter.LanguageId = dto.LanguageId.Value;

      var uploadedUrls = new List<string>();
      bool pagesChanged = false;

      try
      {
        // Parse retained page IDs (comma-separated string from form)
        var retainedIds = new HashSet<int>();
        if (!string.IsNullOrEmpty(dto.RetainedPageIds))
        {
          foreach (var idStr in dto.RetainedPageIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
          {
            if (int.TryParse(idStr.Trim(), out var pid))
              retainedIds.Add(pid);
          }
        }

        // Determine which pages to remove
        var pagesToRemove = retainedIds.Count > 0
            ? chapter.Pages.Where(p => !retainedIds.Contains(p.PageId)).ToList()
            : (newPages != null && newPages.Count > 0 ? chapter.Pages.ToList() : new List<ChapterPage>());

        // Delete removed pages from storage + DB
        if (pagesToRemove.Count > 0)
        {
          foreach (var removedPage in pagesToRemove)
          {
            if (!string.IsNullOrEmpty(removedPage.ImageUrl))
              await _storage.DeleteAsync(removedPage.ImageUrl, ct);
          }
          _db.ChapterPages.RemoveRange(pagesToRemove);
          pagesChanged = true;
        }

        // Upload new pages if provided
        if (newPages != null && newPages.Count > 0)
        {
          var pageFolder = $"chapters/{chapter.ChapterId}/pages";
          var semaphore = new SemaphoreSlim(4);

          var uploadTasks = newPages.Select(async (page, index) =>
          {
            await semaphore.WaitAsync(ct);
            try
            {
              var url = await _storage.UploadAsync(
                            page.FileStream, page.FileName, pageFolder, CancellationToken.None);
              return (url, index);
            }
            finally { semaphore.Release(); }
          });

          var results = await Task.WhenAll(uploadTasks);

          // Determine starting page number (after retained pages)
          var existingMaxPage = retainedIds.Count > 0
              ? chapter.Pages.Where(p => retainedIds.Contains(p.PageId)).Count()
              : 0;

          foreach (var (url, index) in results.OrderBy(r => r.index))
          {
            uploadedUrls.Add(url);
            _db.ChapterPages.Add(new ChapterPage
            {
              ChapterId = chapter.ChapterId,
              PageNumber = existingMaxPage + index + 1,
              ImageUrl = url,
            });
          }
          pagesChanged = true;
        }

        // Renumber remaining pages sequentially
        if (pagesChanged && retainedIds.Count > 0)
        {
          var remainingPages = chapter.Pages
              .Where(p => retainedIds.Contains(p.PageId))
              .OrderBy(p => p.PageNumber)
              .ToList();
          for (int i = 0; i < remainingPages.Count; i++)
            remainingPages[i].PageNumber = i + 1;
        }

        // Update page count
        if (pagesChanged)
        {
          var totalPages = (retainedIds.Count > 0
              ? chapter.Pages.Count(p => retainedIds.Contains(p.PageId))
              : 0) + (newPages?.Count ?? 0);
          chapter.PageCount = totalPages > 0 ? totalPages : chapter.Pages.Count - pagesToRemove.Count;

          // Reset moderation status + re-queue for AI moderation
          chapter.ModerationStatus = ModerationStatus.PENDING;
          chapter.Status = ChapterStatus.DRAFT;
        }

        // ── Đánh dấu Outdated cho các bản dịch ─────────────────────────
        if (chapter.Translations != null && chapter.Translations.Any())
        {
          foreach (var translation in chapter.Translations)
          {
            translation.IsOutdated = true;
          }
        }

        await _db.SaveChangesAsync(ct);

        // Re-enqueue for moderation if pages were updated
        if (pagesChanged)
        {
          await _moderationService.EnqueueChapterForModerationAsync(chapter.ChapterId, ct);
        }

        _logger.LogInformation(
            "Cập nhật chapter thành công. ChapterId: {ChapterId}, Pages: {PageCount}",
            chapter.ChapterId, newPages?.Count ?? chapter.Pages.Count);

        return new CreateChapterResponseDto
        {
          ChapterId = chapter.ChapterId,
          SeriesId = chapter.SeriesId,
          ChapterNumber = chapter.ChapterNumber,
          Title = chapter.Title,
          PageCount = newPages?.Count ?? chapter.Pages.Count,
        };
      }
      catch (Exception ex)
      {
        // Cleanup newly uploaded files on error
        if (uploadedUrls.Count > 0)
        {
          _logger.LogWarning(ex,
              "Update thất bại. Đang xóa {Count} ảnh mới.", uploadedUrls.Count);
          foreach (var url in uploadedUrls)
            await _storage.DeleteAsync(url, ct);
        }
        throw;
      }
    }
  }
}
