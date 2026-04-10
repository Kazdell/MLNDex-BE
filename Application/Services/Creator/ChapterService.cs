using Application.DTOs.Common;
using Application.Exceptions;
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
using Microsoft.Extensions.Caching.Memory;
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
    private readonly IMemoryCache _cache;
    private readonly INotificationService _notificationService;
    private readonly IModerationService _moderationService;

    public ChapterService(
        IMlndexDbContext db,
        IStorageService storage,
        ILogger<ChapterService> logger,
        INotificationService notificationService,
        IModerationService moderationService,
        IMemoryCache cache)
    {
      _db = db;
      _storage = storage;
      _logger = logger;
      _notificationService = notificationService;
      _moderationService = moderationService;
      _cache = cache;
    }

    public async Task<CreateChapterResponseDto> CreateAsync(
    int userId,
    CreateChapterDto dto,
    CancellationToken cancellationToken = default)
    {
      // ── 1. Kiểm tra quyền tải lên (Tác giả hoặc Nhóm dịch) ───────────
      if (dto.TeamId != null)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

      var series = await _db.Series.FirstOrDefaultAsync(s => s.SeriesId == dto.SeriesId && s.Creator.UserId == userId, cancellationToken);
      if (series == null) throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.SERIES_NOT_FOUND);

      // ── 2. Rate Limit: Max 10 chapters/ngày ─────────────────────────
      var todayUtc = DateTime.UtcNow.Date;
      var chaptersToday = await _db.Chapters
          .Where(c => c.Series.Creator.UserId == userId && c.CreatedAt >= todayUtc)
          .CountAsync(cancellationToken);
      if (chaptersToday >= 10)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

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
      //         throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);
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
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

      // ── 4. Kiểm tra trùng số chương (chương gốc) ──
      bool duplicate = await _db.Chapters.AnyAsync(
          c => c.SeriesId == dto.SeriesId
               && c.ChapterNumber == dto.ChapterNumber
               && c.TeamId == null,
          cancellationToken);

      if (duplicate)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

      // ── 3. Upload ảnh trang lên Cloudinary ────────────────────────
      var uploadedUrls = new List<string>();
      var folder = $"chapters/{dto.SeriesId}";

      try
      {
        // ── 4. Build chapter entity ───────────────────────────────
        var creatorProfile = await _db.CreatorProfiles
            .FirstOrDefaultAsync(cp => cp.UserId == userId, cancellationToken);

        var lockStatus = dto.LockStatus ?? (creatorProfile?.UnlockEnabled == true
                                ? ChapterLockStatus.LOCKED
                                : ChapterLockStatus.UNLOCKED);

        var unlockCoins = lockStatus == ChapterLockStatus.LOCKED
                                ? (dto.UnlockPriceCoins ?? creatorProfile?.DefaultUnlockPriceCoins)
                                : null;

        var unlockTime = lockStatus == ChapterLockStatus.LOCKED && creatorProfile?.FreeAfterEnabled == true
                                ? (dto.FreeAfterDays.HasValue
                                    ? DateTime.UtcNow.AddDays(dto.FreeAfterDays.Value)
                                    : creatorProfile.DefaultFreeAfterDays.HasValue
                                        ? DateTime.UtcNow.AddDays(creatorProfile.DefaultFreeAfterDays.Value)
                                        : (DateTime?)null)
                                : null;

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
          CreatedAt = DateTime.UtcNow,

          // ── Unlock settings ──────────────────────────
          LockStatus = lockStatus,
          UnlockPriceCoins = unlockCoins,
          UnlockTime = unlockTime,
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
int chapterId, int? userId, int? translationId = null,
CancellationToken cancellationToken = default)
    {
      if (userId.HasValue)
      {
          return await GetChapterDetailInternalAsync(chapterId, userId, translationId, cancellationToken);
      }

      var cacheKey = $"ChapterDetails_{chapterId}_Trans_{translationId ?? 0}_User_0";
      return await _cache.GetOrCreateAsync(cacheKey, async entry =>
      {
          entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
          return await GetChapterDetailInternalAsync(chapterId, null, translationId, cancellationToken);
      });
    }

    private async Task<ChapterDetailDto?> GetChapterDetailInternalAsync(
int chapterId, int? userId, int? translationId = null,
CancellationToken cancellationToken = default)
    {
      {

        var chapter = await _db.Chapters
            .Include(c => c.Series)
                .ThenInclude(s => s.Creator)
            .Include(c => c.Team)
            .Include(c => c.Pages.OrderBy(p => p.PageNumber))
            .AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(c => c.ChapterId == chapterId, cancellationToken);

        // ── FALLBACK: If no Chapter found, try finding a Translation by TranslationId ──
        if (chapter == null)
        {
          return await GetTranslationAsChapterDetailAsync(chapterId, cancellationToken);
        }

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

        var now = DateTime.UtcNow;
        var effectiveLockStatus = chapter.LockStatus;

        // Lazy check: đã qua UnlockTime → coi như free
        if (chapter.LockStatus == ChapterLockStatus.LOCKED
            && chapter.UnlockTime.HasValue
            && now >= chapter.UnlockTime.Value)
        {
          effectiveLockStatus = ChapterLockStatus.UNLOCKED;
        }

        bool isUnlockedByUser = false;
        if (userId.HasValue && effectiveLockStatus == ChapterLockStatus.LOCKED)
        {
          isUnlockedByUser = await _db.ChapterUnlocks
              .AnyAsync(u => u.ChapterId == chapterId
                          && u.UserId == userId.Value
                          && (
                              u.TranslationId == null // đã mua chapter gốc → mở tất cả
                              || (translationId.HasValue && u.TranslationId == translationId.Value) // hoặc đã mua đúng translation này
                          ),
                        cancellationToken);
        }
        bool isSeriesCreator = userId.HasValue && chapter.Series?.CreatorId != null
&& chapter.Series.Creator?.UserId == userId.Value;


                // 2. QUAN TRỌNG: Ghi đè LockStatus trả về cho Frontend
                // Nếu đã mua (isUnlockedByUser) hoặc là Creator, thì status trả về PHẢI LÀ UNLOCKED
                var finalStatus = (effectiveLockStatus == ChapterLockStatus.UNLOCKED || isUnlockedByUser || isSeriesCreator)
                                  ? ChapterLockStatus.UNLOCKED.ToString()
                                  : ChapterLockStatus.LOCKED.ToString();

        var dto = new ChapterDetailDto
        {
          ChapterId = chapter.ChapterId,
          SeriesId = chapter.SeriesId,
          SeriesTitle = chapter.Series?.Title,
          UploaderName = chapter.Series?.Creator?.PenName,
          CreatorUserId = chapter.Series?.Creator?.UserId,
          TranslatorTeamName = chapter.Team?.TeamName,
          ChapterNumber = chapter.ChapterNumber,
          Title = chapter.Title,
          PrevChapterId = prevChapterId,
          NextChapterId = nextChapterId,
          Chapters = chapters,
                    LockStatus = finalStatus,
          UnlockPriceCoins = effectiveLockStatus == ChapterLockStatus.LOCKED ? chapter.UnlockPriceCoins : null,
          UnlockTime = effectiveLockStatus == ChapterLockStatus.LOCKED ? chapter.UnlockTime : null,
          IsUnlockedByUser = isUnlockedByUser || isSeriesCreator,

          // Chặn Pages nếu locked và user chưa unlock
          Pages = (effectiveLockStatus == ChapterLockStatus.UNLOCKED || isUnlockedByUser || isSeriesCreator)
            ? chapter.Pages.Select(p => new ChapterPageResponseDto
            {
              PageId = p.PageId,
              ChapterId = p.ChapterId,
              PageNumber = p.PageNumber,
              ImageUrl = p.ImageUrl
            }).ToList()
            : new List<ChapterPageResponseDto>(),
        };

        // Translation Ecosystem logic:
        if (chapter.TeamId != null)
        {
          var translation = await _db.Translations
              .Include(t => t.TranslationCredits).ThenInclude(tc => tc.User)
              .Include(t => t.TeamJoins).ThenInclude(tj => tj.Team)
              .Include(t => t.Permission).ThenInclude(p => p!.Team) // ← thêm
              .FirstOrDefaultAsync(t => t.ChapterId == chapter.ChapterId, cancellationToken);

          if (translation != null)
          {
            dto.IsTranslation = true;
            dto.IsOfficial = translation.IsOfficial;
            dto.IsOutdated = translation.IsOutdated;
            dto.IsOrphan = translation.IsOrphan;
            dto.TeamUnlockPrice = translation.Permission?.Team?.DefaultUnlockPriceCoins
                                  ?? chapter.UnlockPriceCoins; // ← thêm

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
    }

    /// <summary>
    /// Fallback: Load a Translation by TranslationId and map to ChapterDetailDto
    /// so the ChapterViewer can display translation pages seamlessly.
    /// </summary>
    private async Task<ChapterDetailDto?> GetTranslationAsChapterDetailAsync(
        int translationId, CancellationToken ct)
    {
      var translation = await _db.Translations
          .Include(t => t.Chapter)
              .ThenInclude(c => c.Series)
                  .ThenInclude(s => s.Creator)
          .Include(t => t.Language)
          .Include(t => t.Permission)
              .ThenInclude(p => p!.Team)
          .Include(t => t.TranslationPages.OrderBy(p => p.PageNumber))
          .Include(t => t.TranslationCredits)
              .ThenInclude(tc => tc.User)
          .Include(t => t.TeamJoins)
              .ThenInclude(tj => tj.Team)
          .FirstOrDefaultAsync(t => t.TranslationId == translationId, ct);

      if (translation == null) return null;

      var chapter = translation.Chapter;
      if (chapter == null) return null;

      // Build chapter list for the series (translations from the same team)
      var chapters = await _db.Translations
          .Include(t => t.Chapter)
          .Include(t => t.Language)
          .Include(t => t.Permission)
              .ThenInclude(p => p!.Team)
          .Where(t => t.Chapter.SeriesId == chapter.SeriesId
                   && t.Permission != null
                   && t.Permission!.TeamId == translation.Permission!.TeamId
                   && t.Chapter.Status == ChapterStatus.PUBLISHED)
          .OrderByDescending(t => t.Chapter.ChapterNumber)
          .Select(t => new ChapterSummaryDto
          {
            ChapterId = t.Chapter.ChapterId,
            TranslationId = t.TranslationId,
            ChapterNumber = t.Chapter.ChapterNumber,
            Title = t.Chapter.Title,
            TeamId = t.Permission!.TeamId,
            TeamName = t.Permission.Team != null ? t.Permission.Team.TeamName : null,
            LanguageCode = t.Language != null ? t.Language.Code : null,
            LanguageName = t.Language != null ? t.Language.Name : null,
            IsOriginal = false
          })
          .ToListAsync(ct);

      var prevTranslationId = chapters
          .Where(c => c.ChapterNumber < chapter.ChapterNumber)
          .OrderByDescending(c => c.ChapterNumber)
          .Select(c => c.TranslationId)
          .FirstOrDefault();

      var nextTranslationId = chapters
          .Where(c => c.ChapterNumber > chapter.ChapterNumber)
          .OrderBy(c => c.ChapterNumber)
          .Select(c => c.TranslationId)
          .FirstOrDefault();

      var dto = new ChapterDetailDto
      {
        ChapterId = translationId,
        SeriesId = chapter.SeriesId,
        SeriesTitle = chapter.Series?.Title,
        UploaderName = chapter.Series?.Creator?.PenName,
        TranslatorTeamName = translation.Permission?.Team?.TeamName,
        ChapterNumber = chapter.ChapterNumber,
        Title = chapter.Title,
        PrevChapterId = prevTranslationId,
        NextChapterId = nextTranslationId,
        Chapters = chapters,
        IsTranslation = true,
        IsOfficial = translation.IsOfficial,
        IsOutdated = translation.IsOutdated,
        IsOrphan = translation.IsOrphan,
        TeamUnlockPrice = translation.Permission?.Team?.DefaultUnlockPriceCoins
                ?? chapter.UnlockPriceCoins,
        Pages = translation.TranslationPages.Select(p => new ChapterPageResponseDto
        {
          PageId = p.TransPageId,
          ChapterId = translationId,
          PageNumber = p.PageNumber,
          ImageUrl = p.TranslationImageUrl
        }).ToList()
      };

      // Translation credits
      if (translation.TranslationCredits != null && translation.TranslationCredits.Any())
      {
        dto.TranslationCredits = translation.TranslationCredits.Select(tc => new TranslationCreditDetailDto
        {
          UserId = tc.UserId,
          Username = tc.User.Username,
          Role = tc.Role.ToString()
        }).ToList();
      }

      // Joint teams
      if (translation.TeamJoins != null && translation.TeamJoins.Any())
      {
        dto.JointTeams = translation.TeamJoins.Select(tj => new JointTeamDetailDto
        {
          TeamId = tj.TeamId,
          TeamName = tj.Team.TeamName
        }).ToList();
      }

      return dto;
    }

    public async Task<List<ChapterListItemDto>> GetBySeriesAsync(int seriesId, int userId, CancellationToken ct = default)
    {
      // Verify ownership
      var series = await _db.Series
          .Include(s => s.Creator)
          .FirstOrDefaultAsync(s => s.SeriesId == seriesId, ct)
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.SERIES_NOT_FOUND);

      if (series.Creator.UserId != userId)
        throw new UnauthorizedAccessException("Bạn không có quyền xem chapters của series này.");

      return await _db.Chapters
          .Where(c => c.SeriesId == seriesId)
          // Creator chapters typically shouldn't have TeamId set, but to be sure we only get original or all?
          // The current system gets all. We keep it as is.
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

    public async Task<List<ChapterListItemDto>> GetTeamChaptersBySeriesAsync(int teamId, int seriesId, int userId, CancellationToken ct = default)
    {
      // Verify team membership
      var isMember = await _db.TeamMembers
          .AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId, ct);

      if (!isMember)
        throw new UnauthorizedAccessException("Bạn không phải là thành viên của nhóm dịch này.");

      // Check if series exists
      var seriesExists = await _db.Series.AnyAsync(s => s.SeriesId == seriesId, ct);
      if (!seriesExists)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.SERIES_NOT_FOUND);

      return await _db.Chapters
          .Where(c => c.SeriesId == seriesId && c.TeamId == teamId)
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
          .AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(c => c.ChapterId == chapterId, ct);

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

      // Load creator defaults to send alongside chapter data
      var creatorProfile = await _db.CreatorProfiles
          .FirstOrDefaultAsync(cp => cp.UserId == userId, ct);

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

        // ── Unlock settings (current chapter values) ──────────────
        LockStatus = chapter.LockStatus.ToString(),
        UnlockPriceCoins = chapter.UnlockPriceCoins,
        UnlockTime = chapter.UnlockTime,

        // ── Creator defaults (for pre-filling the form) ────────────
        CreatorDefaults = creatorProfile == null ? null : new CreatorUnlockDefaultsDto
        {
          UnlockEnabled = creatorProfile.UnlockEnabled,
          DefaultUnlockPriceCoins = creatorProfile.DefaultUnlockPriceCoins,
          FreeAfterEnabled = creatorProfile.FreeAfterEnabled,
          DefaultFreeAfterDays = creatorProfile.DefaultFreeAfterDays,
        },

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
          .AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(c => c.ChapterId == chapterId, ct)
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.CHAPTER_NOT_FOUND);

      // Ownership check
      if (chapter.Series?.Creator?.UserId != userId)
        throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa chương này.");

      // Update metadata
      chapter.ChapterNumber = dto.ChapterNumber;
      chapter.Title = dto.Title;

      // ── Apply lock settings if explicitly provided ─────────────────
      if (dto.LockStatus.HasValue)
      {
        chapter.LockStatus = dto.LockStatus.Value;

        if (dto.LockStatus.Value == ChapterLockStatus.UNLOCKED)
        {
          // Clear everything when explicitly unlocking
          chapter.UnlockPriceCoins = null;
          chapter.UnlockTime = null;
        }
        else
        {
          // Only override each field if the DTO actually sent a value
          if (dto.UnlockPriceCoins.HasValue)
            chapter.UnlockPriceCoins = dto.UnlockPriceCoins.Value;

          if (dto.FreeAfterDays.HasValue)
            chapter.UnlockTime = DateTime.UtcNow.AddDays(dto.FreeAfterDays.Value);
          else if (dto.UnlockTime.HasValue)
            chapter.UnlockTime = dto.UnlockTime.Value;
          // If neither is provided, leave existing UnlockTime untouched
        }
      }
      // If dto.LockStatus is null → creator didn't touch the lock section → preserve existing values

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

    // Update Chapter Lock status
    public async Task<UpdateChapterLockResponseDto> UpdateChapterLockStatusAsync(
        int chapterId, int requestingUserId, UpdateChapterLockDto dto, CancellationToken ct = default)
    {
      // 1. Load chapter + verify ownership through series → creator
      var chapter = await _db.Chapters
          .Include(c => c.Series)
          .AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(c => c.ChapterId == chapterId, ct)
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.CHAPTER_NOT_FOUND);

      // 2. Chỉ creator sở hữu series mới được chỉnh
      var creator = await _db.CreatorProfiles
          .FirstOrDefaultAsync(c => c.UserId == requestingUserId, ct)
          ?? throw new UnauthorizedAccessException("Bạn không phải nhà sáng tạo.");

      if (chapter.Series.CreatorId != creator.CreatorId)
        throw new UnauthorizedAccessException("Bạn không sở hữu chapter này.");

      // 3. Validate: LOCKED phải có ít nhất một trong hai
      if (dto.LockStatus == ChapterLockStatus.LOCKED
          && dto.UnlockPriceCoins == null
          && dto.UnlockTime == null)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

      // 4. UNLOCKED thì clear hết
      if (dto.LockStatus == ChapterLockStatus.UNLOCKED)
      {
        dto.UnlockPriceCoins = null;
        dto.UnlockTime = null;
      }

      // 5. Apply
      chapter.LockStatus = dto.LockStatus;
      chapter.UnlockPriceCoins = dto.UnlockPriceCoins;
      chapter.UnlockTime = dto.UnlockTime;
      chapter.UpdatedAt = DateTime.UtcNow;

      await _db.SaveChangesAsync(ct);

      return new UpdateChapterLockResponseDto
      {
        ChapterId = chapter.ChapterId,
        LockStatus = chapter.LockStatus.ToString(),
        UnlockPriceCoins = chapter.UnlockPriceCoins,
        UnlockTime = chapter.UnlockTime,
      };
    }

    public async Task DeleteAsync(int chapterId, int userId, CancellationToken ct = default)
    {
      var chapter = await _db.Chapters
          .Include(c => c.Series)
              .ThenInclude(s => s.Creator)
          .Include(c => c.Pages)
          .Include(c => c.Translations)
          .AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(c => c.ChapterId == chapterId, ct);

      if (chapter == null)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.CHAPTER_NOT_FOUND);

      if (chapter.Series?.Creator?.UserId != userId)
        throw new UnauthorizedAccessException("Bạn không có quyền xóa chương này.");

            // ── 1. Xóa ChapterUnlock trước (có thể trỏ vào Translation hoặc Chapter) ──
            var unlocks = await _db.ChapterUnlocks
                .Where(u => u.ChapterId == chapterId)
                .ToListAsync(ct);
            _db.ChapterUnlocks.RemoveRange(unlocks);

            // ── 2. Xóa Translation pages từ storage ──────────────────────────────────
            if (chapter.Translations != null)
      {
                var translationIds = chapter.Translations.Select(t => t.TranslationId).ToList();
                var transPages = await _db.TranslationPages
                    .Where(p => translationIds.Contains(p.TranslationId))
                    .ToListAsync(ct);
                foreach (var tp in transPages)
                    await _storage.DeleteAsync(tp.TranslationImageUrl, ct);
      }

            // ── 3. Xóa Chapter pages từ storage ──────────────────────────────────────
            foreach (var page in chapter.Pages)
                if (!string.IsNullOrEmpty(page.ImageUrl))
                    await _storage.DeleteAsync(page.ImageUrl, ct);

            // ── 4. Cascade EF sẽ tự xóa: Translations, ChapterPages, ChapterText ─────
      _db.Chapters.Remove(chapter);
      await _db.SaveChangesAsync(ct);

      _logger.LogInformation("Tác giả {UserId} xóa chương {ChapterId}", userId, chapterId);
    }
    public async Task DeleteTranslationChapterAsync(int chapterId, int teamId, int userId, CancellationToken ct = default)
    {
      var isMember = await _db.TeamMembers
          .AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId, ct);

      if (!isMember)
        throw new UnauthorizedAccessException("Bạn không phải là thành viên của nhóm dịch này.");

      var chapter = await _db.Chapters
          .Include(c => c.Pages)
          .AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(c => c.ChapterId == chapterId && c.TeamId == teamId, ct);

      if (chapter == null)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.TRANSLATION_NOT_FOUND);

            // ── 1. Xóa ChapterUnlock trước ────────────────────────────────────────────
            var unlocks = await _db.ChapterUnlocks
                .Where(u => u.ChapterId == chapterId)
                .ToListAsync(ct);
            _db.ChapterUnlocks.RemoveRange(unlocks);

            // ── 2. Xóa ảnh từ storage ────────────────────────────────────────────────
      foreach (var page in chapter.Pages)
        if (!string.IsNullOrEmpty(page.ImageUrl))
          await _storage.DeleteAsync(page.ImageUrl, ct);

      _db.Chapters.Remove(chapter);
      await _db.SaveChangesAsync(ct);

      _logger.LogInformation("Nhóm {TeamId} xoá chương dịch {ChapterId} bởi User {UserId}", teamId, chapterId, userId);
    }
  }
}
