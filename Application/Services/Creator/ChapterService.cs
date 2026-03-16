using Application.DTOs.AIModeration;
using Application.DTOs.Chapter;
using Application.Interfaces.AIModeration;
using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Application.Interfaces.Notification;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    private readonly IModerationQueue _moderationQueue;
    private readonly IModerationService _moderationService;

    public ChapterService(
        IMlndexDbContext db,
        IStorageService storage,
        ILogger<ChapterService> logger,
        IServiceScopeFactory scopeFactory,
        INotificationService notificationService,
        IModerationQueue moderationQueue,
        IModerationService moderationService)
    {
      _db = db;
      _storage = storage;
      _logger = logger;
      _scopeFactory = scopeFactory;
      _notificationService = notificationService;
      _moderationQueue = moderationQueue;
      _moderationService = moderationService;
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
          LanguageId = dto.LanguageId,
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

          var semaphore = new SemaphoreSlim(4); // Upload song song, tối đa 4 ảnh cùng lúc
          var uploadResults = new (int index, string url)[dto.Pages.Count];

          var uploadTasks = dto.Pages.Select(async (page, index) =>
          {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
              var url = await _storage.UploadAsync(
                  page.FileStream,
                  page.FileName,
                  pageFolder,
                  cancellationToken);
              uploadResults[index] = (index, url);
              uploadedUrls.Add(url);
            }
            finally
            {
              semaphore.Release();
            }
          }).ToArray();

          await Task.WhenAll(uploadTasks);

          foreach (var (index, url) in uploadResults.OrderBy(r => r.index))
          {
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

          // ── Enqueue AI Moderation (crash-safe queue) ────────────────
          await _moderationQueue.EnqueueAsync(
              new ModerationJob(chapter.ChapterId), cancellationToken);
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

    public async Task<ChapterDetailDto?> GetForEditAsync(
        int chapterId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var chapter = await _db.Chapters
            .Include(c => c.Series)
                .ThenInclude(s => s.Creator)
            .Include(c => c.Language)
            .Include(c => c.Pages.OrderBy(p => p.PageNumber))
            .FirstOrDefaultAsync(c => c.ChapterId == chapterId, cancellationToken);

        if (chapter == null) return null;

        // Check permissions: Must be original creator or an authorized team member
        if (chapter.TeamId == null)
        {
            if (chapter.Series.Creator.UserId != userId)
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa chương này (không phải tác giả).");
        }
        else
        {
            var isTeamMember = await _db.TeamMembers.AnyAsync(m => 
                m.TeamId == chapter.TeamId && 
                m.UserId == userId && 
                m.IsActive &&
                (m.Role == TeamMemberRole.LEADER || m.Role == TeamMemberRole.EDITOR || m.Role == TeamMemberRole.TRANSLATOR),
                cancellationToken);
            if (!isTeamMember)
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa chương này (không phải thành viên nhóm dịch).");
        }

        return new ChapterDetailDto
        {
            ChapterId = chapter.ChapterId,
            SeriesId = chapter.SeriesId,
            ChapterNumber = chapter.ChapterNumber,
            Title = chapter.Title,
            Pages = chapter.Pages.Select(p => new ChapterPageResponseDto
            {
                PageId = p.PageId,
                ChapterId = p.ChapterId,
                PageNumber = p.PageNumber,
                ImageUrl = p.ImageUrl
            }).ToList(),
            ModerationStatus = chapter.ModerationStatus.ToString(),
            Language = chapter.Language?.Name,
            ModerationReason = ParseModerationReason(chapter.AiScoresJson)
        };
    }

    public async Task<CreateChapterResponseDto> UpdateAsync(
        int chapterId,
        int userId,
        UpdateChapterDto dto,
        Microsoft.AspNetCore.Http.IFormFileCollection? newPages,
        CancellationToken cancellationToken = default)
    {
        var chapter = await _db.Chapters
            .Include(c => c.Series)
                .ThenInclude(s => s.Creator)
            .Include(c => c.Pages.OrderBy(p => p.PageNumber))
            .FirstOrDefaultAsync(c => c.ChapterId == chapterId, cancellationToken);
        if (chapter == null) throw new KeyNotFoundException($"Không tìm thấy chương {chapterId}.");

        // Check permissions
        if (chapter.TeamId == null)
        {
            if (chapter.Series.Creator.UserId != userId)
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa chương này.");
        }
        else
        {
            var isTeamMember = await _db.TeamMembers.AnyAsync(m => 
                m.TeamId == chapter.TeamId && 
                m.UserId == userId && 
                m.IsActive &&
                (m.Role == TeamMemberRole.LEADER || m.Role == TeamMemberRole.EDITOR || m.Role == TeamMemberRole.TRANSLATOR),
                cancellationToken);
            if (!isTeamMember)
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa chương này.");
        }

        // Check duplicate chapter number if changed
        if (chapter.ChapterNumber != dto.ChapterNumber)
        {
            bool duplicate = await _db.Chapters.AnyAsync(
                c => c.SeriesId == chapter.SeriesId && c.ChapterNumber == dto.ChapterNumber,
                cancellationToken);
            if (duplicate)
                throw new InvalidOperationException($"Chương {dto.ChapterNumber} của truyện này đã tồn tại.");
        }

        // Basic details updates
        chapter.ChapterNumber = dto.ChapterNumber;
        chapter.Title = dto.Title;
        chapter.LanguageId = dto.LanguageId;

        // Process Pages using PageLayoutJson
        var uploadedUrls = new List<string>();
        try
        {
            if (!string.IsNullOrEmpty(dto.PageLayoutJson))
            {
                var layout = global::System.Text.Json.JsonSerializer.Deserialize<List<PageLayoutItem>>(dto.PageLayoutJson);
                if (layout != null)
                {
                    var pageFolder = $"chapters/{chapter.ChapterId}/pages";
                    var updatedPages = new List<ChapterPage>();
                    
                    var existingPagesInfo = chapter.Pages.ToDictionary(p => p.PageId);

                    // Phase 1: Collect new files to upload in parallel
                    var newUploads = new List<(int layoutIndex, int fileIndex)>();
                    for (int i = 0; i < layout.Count; i++)
                    {
                        var item = layout[i];
                        if (item.Type == "new" && item.FileIndex.HasValue &&
                            newPages != null && newPages.Count > item.FileIndex.Value)
                        {
                            newUploads.Add((i, item.FileIndex.Value));
                        }
                    }

                    // Phase 2: Upload new files in parallel (max 4 concurrent)
                    var uploadSemaphore = new SemaphoreSlim(4);
                    var uploadMap = new Dictionary<int, string>(); // layoutIndex -> url
                    
                    if (newUploads.Count > 0)
                    {
                        var uploadTasks = newUploads.Select(async u =>
                        {
                            await uploadSemaphore.WaitAsync(cancellationToken);
                            try
                            {
                                var fileToUpload = newPages![u.fileIndex];
                                using var stream = fileToUpload.OpenReadStream();
                                var url = await _storage.UploadAsync(
                                    stream,
                                    fileToUpload.FileName,
                                    pageFolder,
                                    cancellationToken);
                                lock (uploadMap) { uploadMap[u.layoutIndex] = url; }
                                lock (uploadedUrls) { uploadedUrls.Add(url); }
                            }
                            finally
                            {
                                uploadSemaphore.Release();
                            }
                        }).ToArray();

                        await Task.WhenAll(uploadTasks);
                    }

                    // Phase 3: Build final page list
                    for (int i = 0; i < layout.Count; i++)
                    {
                        var item = layout[i];
                        if (item.Type == "existing")
                        {
                            if (item.Id.HasValue && existingPagesInfo.TryGetValue(item.Id.Value, out var existingPage))
                            {
                                existingPage.PageNumber = i + 1;
                                updatedPages.Add(existingPage);
                                existingPagesInfo.Remove(item.Id.Value);
                            }
                        }
                        else if (item.Type == "new" && uploadMap.TryGetValue(i, out var newUrl))
                        {
                            updatedPages.Add(new ChapterPage
                            {
                                ChapterId = chapter.ChapterId,
                                PageNumber = i + 1,
                                ImageUrl = newUrl
                            });
                        }
                    }

                    // Delete abandoned pages from Cloudinary and DB
                    foreach (var abandonedPage in existingPagesInfo.Values)
                    {
                        if (!string.IsNullOrEmpty(abandonedPage.ImageUrl))
                        {
                            try {
                                await _storage.DeleteAsync(abandonedPage.ImageUrl, cancellationToken);
                            } catch (Exception ex) {
                                _logger.LogWarning(ex, "Failed to delete Cloudinary image: {Url}", abandonedPage.ImageUrl);
                            }
                        }
                    }

                    // Replace pages
                    _db.ChapterPages.RemoveRange(existingPagesInfo.Values);
                    foreach(var p in updatedPages.Where(p => p.PageId == 0)) {
                        _db.ChapterPages.Add(p);
                    }

                    chapter.PageCount = layout.Count;
                }
            }

            // Need to resend to moderation because content might have changed
            chapter.ModerationStatus = ModerationStatus.PENDING;
            chapter.Status = ChapterStatus.DRAFT; // You can also revert this to draft if needed
            chapter.AiScoresJson = null;

            await _db.SaveChangesAsync(cancellationToken);
            
            // Re-enqueue moderation
            await _moderationQueue.EnqueueAsync(new ModerationJob(chapter.ChapterId), cancellationToken);

            return new CreateChapterResponseDto
            {
                ChapterId = chapter.ChapterId,
                SeriesId = chapter.SeriesId,
                ChapterNumber = chapter.ChapterNumber,
                Title = chapter.Title,
                PageCount = chapter.PageCount ?? 0,
            };
        }
        catch (Exception ex)
        {
            // Cleanup any newly uploaded files if DB failed
            if (uploadedUrls.Count > 0)
            {
                _logger.LogWarning(ex, "Lưu file thất bại. Đang xóa {Count} ảnh mới upload.", uploadedUrls.Count);
                foreach (var url in uploadedUrls)
                    await _storage.DeleteAsync(url, cancellationToken);
            }
            throw;
        }
    }

    private static string? ParseModerationReason(string? aiScoresJson)
    {
        if (string.IsNullOrEmpty(aiScoresJson)) return null;
        try
        {
            var scores = global::System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(aiScoresJson);
            if (scores == null || scores.Count == 0) return null;
            // Find the category with the highest score
            var worst = scores.OrderByDescending(kvp => kvp.Value).First();
            if (worst.Value < 0.1) return null; // Below threshold, no meaningful reason
            return $"{worst.Key} ({worst.Value:P0})";
        }
        catch { return null; }
    }

    private class PageLayoutItem
    {
        [global::System.Text.Json.Serialization.JsonPropertyName("type")]
        public string? Type { get; set; } // "existing" or "new"
        
        [global::System.Text.Json.Serialization.JsonPropertyName("id")]
        public int? Id { get; set; } // For "existing"
        
        [global::System.Text.Json.Serialization.JsonPropertyName("fileIndex")]
        public int? FileIndex { get; set; } // For "new"
    }

    // ── Moderation Status & Retry ──────────────────────────────────

    public async Task<ModerationStatusDto> GetModerationStatusAsync(int chapterId)
    {
      return await _moderationService.GetModerationStatusAsync(chapterId);
    }

    public async Task RetryModerationAsync(int chapterId, int userId)
    {
      var chapter = await _db.Chapters
          .Include(c => c.Series)
          .FirstOrDefaultAsync(c => c.ChapterId == chapterId)
          ?? throw new KeyNotFoundException($"Không tìm thấy chapter {chapterId}");

      // Only creator or team member can retry
      if (chapter.Series.CreatorId != userId)
      {
        var isTeamMember = chapter.TeamId != null && await _db.TeamMembers.AnyAsync(
            m => m.TeamId == chapter.TeamId && m.UserId == userId && m.IsActive);
        if (!isTeamMember)
          throw new UnauthorizedAccessException("Bạn không có quyền yêu cầu kiểm duyệt lại.");
      }

      // Reset status and re-enqueue
      chapter.ModerationStatus = ModerationStatus.PENDING;
      chapter.AiScoresJson = null;
      await _db.SaveChangesAsync();

      await _moderationQueue.EnqueueAsync(new ModerationJob(chapterId));
      _logger.LogInformation("User {UserId} requested moderation retry for chapter {ChapterId}", userId, chapterId);
    }
  }
}
