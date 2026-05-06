using Application.DTOs.Creator;
using Application.Interfaces;
using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Application.Interfaces.AIModeration;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Services.Creator
{
  public class SeriesService : ISeriesService
  {
    private readonly IMlndexDbContext _context;
    private readonly IStorageService _storage;
    private readonly IModerationService _moderation;
    private readonly ILogger<SeriesService> _logger;
    private readonly IMemoryCache _cache;

    public SeriesService(
        IMlndexDbContext context,
        IStorageService storage,
        IModerationService moderation,
        ILogger<SeriesService> logger,
        IMemoryCache cache)
    {
      _context = context;
      _storage = storage;
      _moderation = moderation;
      _logger = logger;
      _cache = cache;
    }

    /// <summary>
    /// Tạo mới một bộ truyện (Series). Process bao gồm: kiểm tra Rate Limit (max 5 truyện/ngày), 
    /// check trùng lặp tiêu đề/mô tả, kiểm tra từ ngữ vi phạm qua AI Moderation,
    /// upload ảnh bìa và lưu thông tin vào DB. Series mới tạo sẽ có trạng thái Moderation là PENDING.
    /// </summary>
    public async Task<CreateSeriesResponseDto> CreateAsync(
        int userId,
        CreateSeriesDto dto,
        CancellationToken cancellationToken = default)
    {
      var creator = await _context.CreatorProfiles
          .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

      // 1. Rate Limit: Max 5 series/ngày
      var todayUtc = DateTime.UtcNow.Date;
      var seriesToday = await _context.Series
          .Where(s => s.Creator.UserId == userId && s.CreatedAt >= todayUtc)
          .CountAsync(cancellationToken);
      if (seriesToday >= 5)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

      // 2. Check Title Duplicate
      var titleExists = await _context.Series.AnyAsync(s => s.Title.ToLower() == dto.Title.ToLower(), cancellationToken);
      if (titleExists)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.DUPLICATE_SERIES_TITLE);

      // 1b. Check Description Duplicate
      if (!string.IsNullOrEmpty(dto.Description))
      {
        var descExists = await _context.Series.AnyAsync(s => s.Description == dto.Description, cancellationToken);
        if (descExists)
          _logger.LogWarning("Mô tả truyện bị trùng lặp với một truyện khác.");
      }

      // 2. Check Blacklist for Title & Description
      var titleCheck = await _moderation.PreCheckTextAsync(new DTOs.Moderation.TextCheckRequest { Text = dto.Title });
      if (titleCheck.Action == "AutoReject" || titleCheck.Action == "InstantBan")
      {
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.PROHIBITED_CONTENT);
      }

      if (!string.IsNullOrEmpty(dto.Description))
      {
        var descCheck = await _moderation.PreCheckTextAsync(new DTOs.Moderation.TextCheckRequest { Text = dto.Description });
        if (descCheck.Action == "AutoReject" || descCheck.Action == "InstantBan")
          throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.PROHIBITED_CONTENT);
      }

      string? imageUrl = null;
      if (dto.CoverImage != null)
      {
        imageUrl = await _storage.UploadAsync(
            dto.CoverImage.OpenReadStream(),
            dto.CoverImage.FileName,
            "covers/novels",
            cancellationToken);
      }

      try
      {
        var ageRating = CalculateAgeRating(
            dto.Violence, dto.Nudity, dto.SexualContent,
            dto.LanguageScore, dto.Substances, dto.SensitiveContent);

        var series = new Series
        {
          CreatorId = creator.CreatorId,
          Title = dto.Title,
          Description = dto.Description,
          CoverImageUrl = imageUrl,
          SeriesFormat = SeriesFormat.NOVEL,
          AgeRating = ageRating,
          ViolenceScore = dto.Violence,
          NudityScore = dto.Nudity,
          SexualScore = dto.SexualContent,
          LanguageScore = dto.LanguageScore,
          SubstancesScore = dto.Substances,
          SensitiveScore = dto.SensitiveContent,
          Status = SeriesStatus.ONGOING,
          ModerationStatus = ModerationStatus.PENDING,
          AverageRating = 0,
          TotalRatings = 0,
          CreatedAt = DateTime.UtcNow,
        };

        _context.Series.Add(series);
        await _context.SaveChangesAsync(cancellationToken);

        if (dto.GenreIds != null && dto.GenreIds.Any())
        {
          var seriesGenres = dto.GenreIds.Select(genreId => new SeriesGenre
          {
            SeriesId = series.SeriesId,
            GenreId = genreId
          }).ToList();

          _context.SeriesGenres.AddRange(seriesGenres);
          await _context.SaveChangesAsync(cancellationToken);
        }
        await _moderation.EnqueueSeriesForModerationAsync(
            series.SeriesId, cancellationToken);
        _logger.LogInformation(
            "Tạo novel thành công. SeriesId: {SeriesId}, Title: {Title}, UserId: {UserId}.",
            series.SeriesId, series.Title, userId);

        return new CreateSeriesResponseDto
        {
          SeriesId = series.SeriesId,
          Title = series.Title,
          CoverImageUrl = series.CoverImageUrl,
          AgeRating = series.AgeRating.ToString(),
          ModerationStatus = series.ModerationStatus.ToString()
        };
      }
      catch (Exception ex)
      {
        if (imageUrl != null)
        {
          _logger.LogWarning(ex, "Lưu DB thất bại. Đang xóa ảnh đã upload: {ImageUrl}", imageUrl);
          await _storage.DeleteAsync(imageUrl, cancellationToken);
        }
        throw;
      }
    }

    public async Task<CreateSeriesResponseDto> UpdateAsync(
        int seriesId,
        int userId,
        CreateSeriesDto dto,
        CancellationToken cancellationToken = default)
    {
      var series = await _context.Series
          .Include(s => s.SeriesGenres)
          .Include(s => s.Creator)
          .FirstOrDefaultAsync(s => s.SeriesId == seriesId, cancellationToken)
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.SERIES_NOT_FOUND);

      if (series.Creator.UserId != userId)
      {
        throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa bộ truyện này.");
      }


      if (await _context.Series.AnyAsync(s => s.Title.ToLower() == dto.Title.ToLower() && s.SeriesId != seriesId, cancellationToken))
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.DUPLICATE_SERIES_TITLE);

      if (!string.IsNullOrEmpty(dto.Description))
      {
        if (await _context.Series.AnyAsync(s => s.Description == dto.Description && s.SeriesId != seriesId, cancellationToken))
          _logger.LogWarning("Mô tả truyện bị trùng lặp với một truyện khác.");
      }

      var titleCheck = await _moderation.PreCheckTextAsync(new DTOs.Moderation.TextCheckRequest { Text = dto.Title });
      if (titleCheck.Action == "AutoReject" || titleCheck.Action == "InstantBan")
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.PROHIBITED_CONTENT);

      if (!string.IsNullOrEmpty(dto.Description))
      {
        var descCheck = await _moderation.PreCheckTextAsync(new DTOs.Moderation.TextCheckRequest { Text = dto.Description });
        if (descCheck.Action == "AutoReject" || descCheck.Action == "InstantBan")
          throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.PROHIBITED_CONTENT);
      }

      string? newImageUrl = null;
      if (dto.CoverImage != null)
      {
        newImageUrl = await _storage.UploadAsync(
            dto.CoverImage.OpenReadStream(),
            dto.CoverImage.FileName,
            "covers/novels",
            cancellationToken);

        if (!string.IsNullOrEmpty(series.CoverImageUrl))
        {
          await _storage.DeleteAsync(series.CoverImageUrl, cancellationToken);
        }
        series.CoverImageUrl = newImageUrl;
      }

      try
      {
        series.AgeRating = CalculateAgeRating(
            dto.Violence, dto.Nudity, dto.SexualContent,
            dto.LanguageScore, dto.Substances, dto.SensitiveContent);

        series.ViolenceScore = dto.Violence;
        series.NudityScore = dto.Nudity;
        series.SexualScore = dto.SexualContent;
        series.LanguageScore = dto.LanguageScore;
        series.SubstancesScore = dto.Substances;
        series.SensitiveScore = dto.SensitiveContent;

        series.Title = dto.Title;
        series.Description = dto.Description;

        // Update series status if provided
        if (!string.IsNullOrEmpty(dto.Status)
            && Enum.TryParse<SeriesStatus>(dto.Status, true, out var newStatus))
        {
          series.Status = newStatus;
        }

        if (dto.GenreIds != null)
        {
          _context.SeriesGenres.RemoveRange(series.SeriesGenres);

          var seriesGenres = dto.GenreIds.Select(genreId => new SeriesGenre
          {
            SeriesId = series.SeriesId,
            GenreId = genreId
          }).ToList();

          _context.SeriesGenres.AddRange(seriesGenres);
        }

        series.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        
        _cache.Remove($"SeriesDetails_{seriesId}_User_0");
        _cache.Remove($"SeriesDetails_{seriesId}_User_{userId}");
        await _moderation.EnqueueSeriesForModerationAsync(
            series.SeriesId, cancellationToken);

        return new CreateSeriesResponseDto
        {
          SeriesId = series.SeriesId,
          Title = series.Title,
          CoverImageUrl = series.CoverImageUrl,
          AgeRating = series.AgeRating.ToString(),
          ModerationStatus = series.ModerationStatus.ToString()
        };
      }
      catch (Exception ex)
      {
        if (newImageUrl != null)
        {
          _logger.LogWarning(ex, "Cập nhật thất bại. Đang xóa ảnh mới upload: {ImageUrl}", newImageUrl);
          await _storage.DeleteAsync(newImageUrl, cancellationToken);
        }
        throw;
      }
    }

    public async Task<List<SeriesListItemDto>> GetByCreatorAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
      var creator = await _context.CreatorProfiles
          .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

      var series = await _context.Series
          .Where(s => s.CreatorId == creator.CreatorId)
          .Include(s => s.Chapters)
          .ThenInclude(c => c.ReadingHistories)
          .Include(s => s.SeriesGenres)
          .ThenInclude(sg => sg.Genre)
          .OrderByDescending(s => s.CreatedAt)
          .AsNoTracking()
          .AsSplitQuery()
          .ToListAsync(cancellationToken);

      return series.Select(s => new SeriesListItemDto
      {
        SeriesId = s.SeriesId,
        Title = s.Title,
        CoverImageUrl = s.CoverImageUrl,
        LastChapterNumber = s.Chapters.Any()
                    ? (float)s.Chapters.Max(c => c.ChapterNumber)
                    : 0,
        ChapterCount = s.Chapters.Count,

        TotalViews = s.Chapters.Sum(c => (long)c.Views),

        Status = s.Status.ToString(),
        ModerationStatus = s.ModerationStatus.ToString(),
        AgeRating = s.AgeRating.ToString(),
        LastUpdatedAt = s.Chapters.Any(c => c.PublishedAt.HasValue)
            ? s.Chapters.Where(c => c.PublishedAt.HasValue).Max(c => c.PublishedAt)
            : s.CreatedAt,
        Genres = s.SeriesGenres.Select(sg => sg.Genre.Name).ToList(),
      }).ToList();
    }
    public async Task<PaginatedList<SeriesDto>> GetSeriesListAsync(string sortBy = "newest", int page = 1, int pageSize = 20)
    {
      var cacheKey = $"SeriesList_{sortBy}_{page}_{pageSize}";
      return await _cache.GetOrCreateAsync(cacheKey, async entry =>
      {
          entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
          return await GetSeriesListInternalAsync(sortBy, page, pageSize);
      }) ?? new PaginatedList<SeriesDto>();
    }

    private async Task<PaginatedList<SeriesDto>> GetSeriesListInternalAsync(string sortBy = "newest", int page = 1, int pageSize = 20)
    {
      var query = _context.Series
          .Where(s => (s.Status == SeriesStatus.ONGOING || s.Status == SeriesStatus.COMPLETED) && s.ModerationStatus == ModerationStatus.APPROVED)
          .AsQueryable();

      if (sortBy.Equals("popular", StringComparison.OrdinalIgnoreCase))
        query = query.OrderByDescending(s => s.Chapters.Sum(c => (long)c.Views));
      else if (sortBy.Equals("newest", StringComparison.OrdinalIgnoreCase))
        query = query.OrderByDescending(s => s.Chapters!.Where(c => c.PublishedAt.HasValue).OrderByDescending(c => c.ChapterNumber).Select(c => c.PublishedAt).FirstOrDefault() ?? s.CreatedAt);
      else
        query = query.OrderByDescending(s => s.CreatedAt);

      var totalCount = await query.CountAsync();
      var seriesData = await query
          .Select(s => new { 
              s.SeriesId,
              TotalViews = s.Chapters.Sum(c => (long)c.Views)
          })
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .ToListAsync();

      var ids = seriesData.Select(x => x.SeriesId).ToList();
      var totalViewsDict = seriesData.ToDictionary(x => x.SeriesId, x => (long)x.TotalViews);

      if (!ids.Any()) return new PaginatedList<SeriesDto> { TotalCount = totalCount, Page = page, PageSize = pageSize, Items = new List<SeriesDto>() };

      var items = await _context.Series
          .Include(s => s.Creator)
          .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)

          .Include(s => s.Chapters.Where(c => c.Status == ChapterStatus.PUBLISHED && c.ModerationStatus == ModerationStatus.APPROVED).OrderByDescending(c => c.ChapterNumber).Take(2)).ThenInclude(c => c.Translations.Where(t => t.QualityStatus == TranslationQualityStatus.PUBLISHED && t.ModerationStatus == ModerationStatus.APPROVED))
          .Where(s => ids.Contains(s.SeriesId))
          .AsNoTracking()
          .AsSplitQuery()
          .ToListAsync();

      // Preload granted team permissions for all series in this batch
      var grantedPermsDict = await _context.TranslationPermissions
          .Where(p => ids.Contains(p.SeriesId) && p.Status == TranslationPermissionStatus.GRANTED)
          .GroupBy(p => p.SeriesId)
          .ToDictionaryAsync(g => g.Key, g => g.Select(p => p.TeamId).ToHashSet());

      var orderedItems = ids.Select(id => items.FirstOrDefault(i => i.SeriesId == id)).Where(i => i != null).ToList();

      return new PaginatedList<SeriesDto>
      {
        TotalCount = totalCount,
        Page = page,
        PageSize = pageSize,
        Items = orderedItems.Select(s => MapToDto(s!, grantedPermsDict.GetValueOrDefault(s!.SeriesId), totalViewsDict.GetValueOrDefault(s!.SeriesId))).ToList()
      };
    }

    public async Task<PaginatedList<SeriesDto>> SearchSeriesAsync(SeriesSearchRequest request)
    {
      var query = _context.Series
          .Where(s => s.ModerationStatus == ModerationStatus.APPROVED)
          .AsQueryable();

      if (!string.IsNullOrWhiteSpace(request.Keyword))
      {
        query = query.Where(s => s.Title.Contains(request.Keyword) ||
                                 (s.Creator != null && s.Creator.PenName.Contains(request.Keyword)));
      }
      if (request.GenreId.HasValue)
        query = query.Where(s => s.SeriesGenres.Any(sg => sg.GenreId == request.GenreId.Value));

      if (request.Status.HasValue)
        query = query.Where(s => s.Status == request.Status.Value);

      if (request.Format.HasValue)
        query = query.Where(s => s.SeriesFormat == request.Format.Value);

      if (request.CreatorId.HasValue)
        query = query.Where(s => s.CreatorId == request.CreatorId.Value);

      if (request.ExcludeSeriesId.HasValue)
        query = query.Where(s => s.SeriesId != request.ExcludeSeriesId.Value);

      if (request.YearFrom.HasValue)
        query = query.Where(s => s.CreatedAt.Year >= request.YearFrom.Value);

      if (request.YearTo.HasValue)
        query = query.Where(s => s.CreatedAt.Year <= request.YearTo.Value);

      if (request.MinRating.HasValue)
        query = query.Where(s => s.AverageRating >= request.MinRating.Value);

      if (string.Equals(request.SortBy, "popular", StringComparison.OrdinalIgnoreCase))
        query = query.OrderByDescending(s => s.Chapters.Sum(c => (long)c.Views));
      else if (string.Equals(request.SortBy, "newest", StringComparison.OrdinalIgnoreCase))
        query = query.OrderByDescending(s => s.Chapters!.Where(c => c.PublishedAt.HasValue).OrderByDescending(c => c.ChapterNumber).Select(c => c.PublishedAt).FirstOrDefault() ?? s.CreatedAt);
      else
        query = query.OrderByDescending(s => s.CreatedAt);

      var totalCount = await query.CountAsync();
      var seriesData = await query
          .Select(s => new { 
              s.SeriesId,
              TotalViews = s.Chapters.Sum(c => (long)c.Views)
          })
          .Skip((request.Page - 1) * request.PageSize)
          .Take(request.PageSize)
          .ToListAsync();

      var ids = seriesData.Select(x => x.SeriesId).ToList();
      var totalViewsDict = seriesData.ToDictionary(x => x.SeriesId, x => (long)x.TotalViews);

      if (!ids.Any()) return new PaginatedList<SeriesDto> { TotalCount = totalCount, Page = request.Page, PageSize = request.PageSize, Items = new List<SeriesDto>() };

      var items = await _context.Series
          .Include(s => s.Creator)
          .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)

          .Include(s => s.Chapters.Where(c => c.Status == ChapterStatus.PUBLISHED && c.ModerationStatus == ModerationStatus.APPROVED).OrderByDescending(c => c.ChapterNumber).Take(2)).ThenInclude(c => c.Translations.Where(t => t.QualityStatus == TranslationQualityStatus.PUBLISHED && t.ModerationStatus == ModerationStatus.APPROVED))
          .Where(s => ids.Contains(s.SeriesId))
          .AsNoTracking()
          .AsSplitQuery()
          .ToListAsync();

      // Preload granted team permissions for all series in this batch
      var grantedPermsDict = await _context.TranslationPermissions
          .Where(p => ids.Contains(p.SeriesId) && p.Status == TranslationPermissionStatus.GRANTED)
          .GroupBy(p => p.SeriesId)
          .ToDictionaryAsync(g => g.Key, g => g.Select(p => p.TeamId).ToHashSet());

      var orderedItems = ids.Select(id => items.FirstOrDefault(i => i.SeriesId == id)).Where(i => i != null).ToList();

      return new PaginatedList<SeriesDto>
      {
        TotalCount = totalCount,
        Page = request.Page,
        PageSize = request.PageSize,
        Items = orderedItems.Select(s => MapToDto(s!, grantedPermsDict.GetValueOrDefault(s!.SeriesId), totalViewsDict.GetValueOrDefault(s!.SeriesId))).ToList()
      };
    }
    /// Lấy chi tiết thông tin một bộ truyện bao gồm Tác giả, Thể loại, Chương truyện (bao gồm bản dịch).
    /// Xác định xem bản dịch có phải là chính thức hay không dựa vào TranslationPermissions.
    public async Task<SeriesDetailDto?> GetSeriesDetailsAsync(int seriesId, int? userId = null, bool isModOrAdmin = false)
    {
      var cacheKey = $"SeriesDetails_{seriesId}_User_{userId ?? 0}_Mod_{isModOrAdmin}";
      return await _cache.GetOrCreateAsync(cacheKey, async entry =>
      {
          entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
          return await GetSeriesDetailsInternalAsync(seriesId, userId, isModOrAdmin);
      });
    }

    private async Task<SeriesDetailDto?> GetSeriesDetailsInternalAsync(int seriesId, int? userId = null, bool isModOrAdmin = false)
    {
      var series = await _context.Series
          .Include(s => s.Creator)
          .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)

          .Include(s => s.Chapters).ThenInclude(c => c.Pages)
          .Include(s => s.Chapters).ThenInclude(c => c.Translations)
              .ThenInclude(t => t.Language)
          .Include(s => s.Chapters).ThenInclude(c => c.Translations)
              .ThenInclude(t => t.Permission)
                  .ThenInclude(p => p!.Team)
          .AsNoTracking()
          .AsSplitQuery()
          .FirstOrDefaultAsync(s => s.SeriesId == seriesId);

      if (series == null) return null;

      // Restrict viewing series if not approved
      if (series.ModerationStatus != ModerationStatus.APPROVED)
      {
         if (!isModOrAdmin && (!userId.HasValue || series.Creator.UserId != userId.Value))
         {
             return null;
         }
      }

      // Preload granted team IDs for this series
      var grantedTeamIdsList = await _context.TranslationPermissions
          .Where(p => p.SeriesId == seriesId && p.Status == TranslationPermissionStatus.GRANTED)
          .Select(p => p.TeamId)
          .ToListAsync();
      var grantedTeamIds = new HashSet<int>(grantedTeamIdsList);

      var userUnlocks = new List<(int ChapId, int? TransId)>();
      bool isAuthorizedTranslator = false;

      if (userId.HasValue && userId.Value > 0)
      {
        var chapterIds = series.Chapters.Select(c => c.ChapterId).ToList();
        var unlockData = await _context.ChapterUnlocks
            .Where(u => u.UserId == userId.Value && chapterIds.Contains(u.ChapterId))
            .Select(u => new { u.ChapterId, u.TranslationId })
            .ToListAsync();

        userUnlocks = unlockData.Select(u => (u.ChapterId, u.TranslationId)).ToList();

        isAuthorizedTranslator = await _context.TranslationPermissions
            .Include(p => p.Team)
                .ThenInclude(t => t.TeamMembers)
            .AnyAsync(p => p.SeriesId == seriesId
                        && p.Status == TranslationPermissionStatus.GRANTED
                        && (p.Team.LeaderId == userId.Value
                            || p.Team.TeamMembers.Any(tm => tm.UserId == userId.Value && tm.IsActive)));
      }



      // 1. Map original chapters (bản gốc) — PUBLISHED only
      var chapterDtos = series.Chapters
.Where(c => c.Status == ChapterStatus.PUBLISHED && c.ModerationStatus == ModerationStatus.APPROVED)
.OrderByDescending(c => c.ChapterNumber)
.Select(c =>
{
  var now = DateTime.UtcNow;
  var effectiveLock = c.LockStatus;
  if (effectiveLock == ChapterLockStatus.LOCKED
            && c.UnlockTime.HasValue
            && now >= c.UnlockTime.Value)
  {
    effectiveLock = ChapterLockStatus.UNLOCKED;
  }

  return new SeriesChapterDto
  {
    ChapterId = c.ChapterId,
    Title = c.Title ?? "Untitled",
    ChapterNumber = (int)c.ChapterNumber,
    Price = c.UnlockPriceCoins ?? 0,
    PublishedAt = c.PublishedAt ?? DateTime.UtcNow,
    ViewCount = c.Views,
    GroupName = null,
    TeamId = null,
    IsOriginal = true,
    IsOfficialTranslation = false,
    LanguageCode = null,
    LanguageName = null,
    CommentCount = 0,
    PageCount = c.PageCount ?? c.Pages?.Count ?? 0,
    IsUnlockedByUser = userUnlocks.Any(u => u.ChapId == c.ChapterId && u.TransId == null) || isAuthorizedTranslator,
    // ── THÊM 3 DÒNG NÀY ──
    LockStatus = isAuthorizedTranslator ? ChapterLockStatus.UNLOCKED.ToString() : effectiveLock.ToString(),
    UnlockPriceCoins = effectiveLock == ChapterLockStatus.LOCKED ? c.UnlockPriceCoins : null,
    UnlockTime = effectiveLock == ChapterLockStatus.LOCKED ? c.UnlockTime : null,
  };
}).ToList();

      // 2. Map PUBLISHED translations as additional entries
      //    Each Translation maps to the same ChapterNumber so FE can group them together
      var translationDtos = series.Chapters
.SelectMany(c => (c.Translations as IEnumerable<Domain.Entities.Translation> ?? Array.Empty<Domain.Entities.Translation>())
  .Where(t => t.QualityStatus == TranslationQualityStatus.PUBLISHED && t.ModerationStatus == ModerationStatus.APPROVED)
  .Select(t =>
  {
    // Tính effective lock của chapter gốc
    var now = DateTime.UtcNow;
    var effectiveLock = c.LockStatus;
    if (effectiveLock == ChapterLockStatus.LOCKED
              && c.UnlockTime.HasValue
              && now >= c.UnlockTime.Value)
    {
      effectiveLock = ChapterLockStatus.UNLOCKED;
    }

    // TeamUnlockPrice chỉ có giá trị khi chapter gốc đang bị lock
    var teamPrice = effectiveLock == ChapterLockStatus.LOCKED
              ? (t.Permission?.Team?.DefaultUnlockPriceCoins ?? c.UnlockPriceCoins)
              : null;

    // isUnlocked: đã mua chapter gốc → mở tất cả translation
    // hoặc đã mua đúng translation này
    var isUnlocked = userUnlocks.Any(u => u.ChapId == c.ChapterId &&
                 (u.TransId == null || u.TransId == t.TranslationId)) || isAuthorizedTranslator;
    // TODO: nếu muốn check per-translation unlock riêng, cần query ChapterUnlocks theo TranslationId

    return new SeriesChapterDto
    {
      ChapterId = c.ChapterId,
      TranslationId = t.TranslationId,
      Title = c.Title ?? "Untitled",
      ChapterNumber = (int)c.ChapterNumber,
      Price = teamPrice ?? 0,           // giá hiển thị trên badge
      PublishedAt = t.PublishedAt ?? DateTime.UtcNow,
      ViewCount = c.Views,
      GroupName = t.Permission?.Team?.TeamName,
      TeamId = t.Permission?.TeamId,
      IsOriginal = false,
      IsOfficialTranslation = t.IsOfficial
                  || (t.Permission?.TeamId != null && grantedTeamIds.Contains(t.Permission.TeamId)),
      LanguageCode = t.Language?.Code,
      LanguageName = t.Language?.Name,
      CommentCount = 0,
      // ── NEW fields ──
      LockStatus = isAuthorizedTranslator ? ChapterLockStatus.UNLOCKED.ToString() : effectiveLock.ToString(),
      TeamUnlockPrice = teamPrice,
      UnlockTime = effectiveLock == ChapterLockStatus.LOCKED ? c.UnlockTime : null,
      IsUnlockedByUser = isUnlocked,
    };
  }))
.ToList();

      // Merge both lists
      chapterDtos.AddRange(translationDtos);

      return new SeriesDetailDto
      {
        SeriesId = series.SeriesId,
        Title = series.Title,
        Description = series.Description,
        CoverImageUrl = series.CoverImageUrl,
        SeriesFormat = series.SeriesFormat.ToString(),
        AgeRating = series.AgeRating.ToString(),
        Status = series.Status.ToString(),
        AverageRating = series.AverageRating,
        TotalRatings = series.TotalRatings,
        TotalViews = series.Chapters.Sum(c => (long)c.Views),
        CreatedAt = series.CreatedAt,
        CreatorId = series.CreatorId,
        CreatorUserId = series.Creator.UserId,
        CreatorName = series.Creator.PenName,
        Genres = series.SeriesGenres.Select(sg => sg.Genre.Name).ToList(),
        OriginalLanguage = null,
        Chapters = chapterDtos
      };
    }

    public async Task<CreateSeriesDto?> GetForEditAsync(int seriesId, int userId)
    {
      var series = await _context.Series
          .Include(s => s.SeriesGenres)
          .Include(s => s.Creator)
          .FirstOrDefaultAsync(s => s.SeriesId == seriesId);

      if (series == null || series.Creator.UserId != userId) return null;

      return new CreateSeriesDto
      {
        Title = series.Title,
        Description = series.Description,
        CoverImageUrl = series.CoverImageUrl,
        GenreIds = series.SeriesGenres.Select(sg => sg.GenreId).ToList(),
        Violence = series.ViolenceScore,
        Nudity = series.NudityScore,
        SexualContent = series.SexualScore,
        LanguageScore = series.LanguageScore,
        Substances = series.SubstancesScore,
        SensitiveContent = series.SensitiveScore,
        AgeRating = series.AgeRating,
        ModerationStatus = series.ModerationStatus.ToString(),
        Status = series.Status.ToString()
      };
    }


    // ── Status-only update (no moderation, no cooldown) ──────────────────
    public async Task UpdateStatusAsync(
        int seriesId, int userId, string status,
        CancellationToken cancellationToken = default)
    {
      var series = await _context.Series
          .Include(s => s.Creator)
          .FirstOrDefaultAsync(s => s.SeriesId == seriesId, cancellationToken)
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.SERIES_NOT_FOUND);

      if (series.Creator.UserId != userId)
        throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa bộ truyện này.");

      if (!Enum.TryParse<SeriesStatus>(status, true, out var newStatus))
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

      series.Status = newStatus;
      series.UpdatedAt = DateTime.UtcNow;
      await _context.SaveChangesAsync(cancellationToken);

      _logger.LogInformation("Cập nhật trạng thái truyện {SeriesId} → {Status}", seriesId, newStatus);
    }

    public async Task DeleteAsync(int seriesId, int userId, CancellationToken cancellationToken = default)
    {
      var series = await _context.Series
          .Include(s => s.Creator)
          .Include(s => s.Chapters).ThenInclude(c => c.Pages)
          .Include(s => s.SeriesGenres)
          .FirstOrDefaultAsync(s => s.SeriesId == seriesId, cancellationToken)
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.SERIES_NOT_FOUND);

      if (series.Creator.UserId != userId)
      {
        throw new UnauthorizedAccessException("Bạn không có quyền xóa bộ truyện này.");
      }

      try
      {
        // 1. Delete chapter pages' storage files & page records
        foreach (var chapter in series.Chapters)
        {
          foreach (var page in chapter.Pages)
          {
            if (!string.IsNullOrEmpty(page.ImageUrl))
              await _storage.DeleteAsync(page.ImageUrl, cancellationToken);
          }
          _context.ChapterPages.RemoveRange(chapter.Pages);
        }

        // 2. Collect ALL moderation queue entries (chapter + series level)
        var chapterIds = series.Chapters.Select(c => c.ChapterId).ToList();

        var allModerationEntries = await _context.ModerationQueues
            .Where(q => (chapterIds.Contains(q.ContentId) && q.ContentType == ModerationQueueContentType.CHAPTER)
                     || (q.ContentId == seriesId && q.ContentType == ModerationQueueContentType.SERIES))
            .ToListAsync(cancellationToken);

        // 3. Delete Reports FIRST (FK → ModerationQueue.QueueId)
        if (allModerationEntries.Any())
        {
          var queueIds = allModerationEntries.Select(q => q.QueueId).ToList();
          var reports = await _context.Reports
              .Where(r => r.QueueId.HasValue && queueIds.Contains(r.QueueId.Value))
              .ToListAsync(cancellationToken);
          _context.Reports.RemoveRange(reports);
        }

        // 4. Now safe to delete ModerationQueue entries
        _context.ModerationQueues.RemoveRange(allModerationEntries);

        // 4. Delete chapters
        _context.Chapters.RemoveRange(series.Chapters);

        // 5. Delete other FK relationships
        var bookmarks = await _context.Bookmarks
            .Where(b => b.SeriesId == seriesId).ToListAsync(cancellationToken);
        _context.Bookmarks.RemoveRange(bookmarks);

        var ratings = await _context.Ratings
            .Where(r => r.SeriesId == seriesId).ToListAsync(cancellationToken);
        _context.Ratings.RemoveRange(ratings);

        var readingHistories = await _context.ReadingHistories
            .Where(r => r.SeriesId == seriesId).ToListAsync(cancellationToken);
        _context.ReadingHistories.RemoveRange(readingHistories);

        var translationPerms = await _context.TranslationPermissions
            .Where(t => t.SeriesId == seriesId).ToListAsync(cancellationToken);
        _context.TranslationPermissions.RemoveRange(translationPerms);

        // 6. Delete series genres
        _context.SeriesGenres.RemoveRange(series.SeriesGenres);

        // 7. Delete cover image from storage
        if (!string.IsNullOrEmpty(series.CoverImageUrl))
          await _storage.DeleteAsync(series.CoverImageUrl, cancellationToken);

        // 8. Delete series
        _context.Series.Remove(series);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Xóa truyện thành công. SeriesId: {SeriesId}, UserId: {UserId}", seriesId, userId);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Lỗi khi xóa truyện {SeriesId}", seriesId);
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);
      }
    }

    private SeriesDto MapToDto(Series s, HashSet<int>? grantedTeamIds = null, long? totalViewsOverride = null)
    {
      return new SeriesDto
      {
        SeriesId = s.SeriesId,
        Title = s.Title,
        Description = s.Description,
        CoverImageUrl = s.CoverImageUrl,
        SeriesFormat = s.SeriesFormat.ToString(),
        AgeRating = s.AgeRating.ToString(),
        Status = s.Status.ToString(),
        AverageRating = s.AverageRating,
        TotalRatings = s.TotalRatings,
        TotalViews = totalViewsOverride ?? 0,
        CreatedAt = s.CreatedAt,
        CreatorId = s.CreatorId,
        CreatorUserId = s.Creator.UserId,
        CreatorName = s.Creator.PenName,
        Genres = s.SeriesGenres.Select(sg => sg.Genre.Name).ToList(),
        LatestChapters = s.Chapters
              .Where(c => c.Status == ChapterStatus.PUBLISHED && c.ModerationStatus == ModerationStatus.APPROVED)
              .OrderByDescending(c => c.ChapterNumber)
              .Take(2)
              .Select(c => new SeriesChapterDto
              {
                ChapterId = c.ChapterId,
                Title = c.Title ?? "Untitled",
                ChapterNumber = (int)c.ChapterNumber,
                Price = c.UnlockPriceCoins ?? 0,
                PublishedAt = c.PublishedAt ?? DateTime.UtcNow,
                ViewCount = c.Views,
                GroupName = null,
                TeamId = null,
                IsOriginal = true,
                IsOfficialTranslation = (c.Translations != null && c.Translations.Any(t => t.IsOfficial)),
                LanguageCode = null,
                LanguageName = null,
                CommentCount = 0
              }).ToList()
      };
    }

    /// <summary>
    /// Determines if a chapter is an official translation.
    /// A chapter is official if:
    /// 1. It has Translation records with IsOfficial = true, OR
    /// 2. It was uploaded by a team (TeamId != null) that has a GRANTED TranslationPermission for this series.
    /// </summary>
    private static bool IsChapterOfficialTranslation(Chapter c, HashSet<int>? grantedTeamIds)
    {
      if (c.Translations != null && c.Translations.Any(t => t.IsOfficial))
        return true;

      return false;
    }

    private static AgeRating CalculateAgeRating(
        int violence, int nudity, int sexual,
        int language, int substances, int sensitive)
    {
      if (nudity == 3 || sexual == 3)
        return AgeRating.ADULT;

      if (nudity >= 2 || sexual >= 2)
      {
        int total = violence + nudity + sexual + language + substances + sensitive;
        return total >= 12 ? AgeRating.ADULT : AgeRating.MATURE;
      }

      int totalScore = violence + nudity + sexual + language + substances + sensitive;
      return totalScore switch
      {
        0 => AgeRating.ALL_AGES,
        <= 5 => AgeRating.TEEN,
        <= 11 => AgeRating.MATURE,
        _ => AgeRating.ADULT
      };
    }
  }
}
