using Application.DTOs.Creator;
using Application.Interfaces;
using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Application.Interfaces.AIModeration;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services.Creator
{
  public class SeriesService : ISeriesService
  {
    private readonly IMlndexDbContext _context;
    private readonly IStorageService _storage;
    private readonly IModerationService _moderation;
    private readonly ILogger<SeriesService> _logger;

    public SeriesService(
        IMlndexDbContext context,
        IStorageService storage,
        IModerationService moderation,
        ILogger<SeriesService> logger)
    {
      _context = context;
      _storage = storage;
      _moderation = moderation;
      _logger = logger;
    }

    public async Task<CreateSeriesResponseDto> CreateAsync(
        int userId,
        CreateSeriesDto dto,
        CancellationToken cancellationToken = default)
    {
      var creator = await _context.CreatorProfiles
          .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
          ?? throw new KeyNotFoundException($"Creator với UserId {userId} không tồn tại.");

      // 1. Check Title Duplicate
      var titleExists = await _context.Series.AnyAsync(s => s.Title.ToLower() == dto.Title.ToLower());
      if (titleExists)
        throw new ArgumentException("Tiêu đề truyện này đã tồn tại trên hệ thống.");

      // 1b. Check Description Duplicate
      if (!string.IsNullOrEmpty(dto.Description))
      {
        var descExists = await _context.Series.AnyAsync(s => s.Description == dto.Description);
        if (descExists)
            _logger.LogWarning("Mô tả truyện bị trùng lặp với một truyện khác.");
      }

      // 2. Check Blacklist for Title & Description
      var titleCheck = _moderation.PreCheckText(new DTOs.Moderation.TextCheckRequest { Text = dto.Title });
      if (titleCheck.Action == "AutoReject" || titleCheck.Action == "InstantBan")
        throw new ArgumentException($"Tiêu đề vi phạm: {string.Join(", ", titleCheck.Reasons)}");

      if (!string.IsNullOrEmpty(dto.Description))
      {
        var descCheck = _moderation.PreCheckText(new DTOs.Moderation.TextCheckRequest { Text = dto.Description });
        if (descCheck.Action == "AutoReject" || descCheck.Action == "InstantBan")
          throw new ArgumentException($"Mô tả chứa từ ngữ không phù hợp.");
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

        _ = _moderation.RunSeriesModerationAsync(series.SeriesId);

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
          ?? throw new KeyNotFoundException($"Series {seriesId} không tồn tại.");

      if (series.Creator.UserId != userId)
      {
        throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa bộ truyện này.");
      }

      if (await _context.Series.AnyAsync(s => s.Title.ToLower() == dto.Title.ToLower() && s.SeriesId != seriesId))
          throw new ArgumentException("Tiêu đề mới đã tồn tại.");

      if (!string.IsNullOrEmpty(dto.Description))
      {
          if (await _context.Series.AnyAsync(s => s.Description == dto.Description && s.SeriesId != seriesId))
              _logger.LogWarning("Mô tả truyện bị trùng lặp với một truyện khác.");
      }

      var titleCheck = _moderation.PreCheckText(new DTOs.Moderation.TextCheckRequest { Text = dto.Title });
      if (titleCheck.Action == "AutoReject" || titleCheck.Action == "InstantBan")
          throw new ArgumentException($"Tiêu đề vi phạm.");

      if (!string.IsNullOrEmpty(dto.Description))
      {
          var descCheck = _moderation.PreCheckText(new DTOs.Moderation.TextCheckRequest { Text = dto.Description });
          if (descCheck.Action == "AutoReject" || descCheck.Action == "InstantBan")
              throw new ArgumentException($"Mô tả chứa từ ngữ không phù hợp.");
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

        await _context.SaveChangesAsync(cancellationToken);
        _ = _moderation.RunSeriesModerationAsync(series.SeriesId);

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
      return await _context.Series
          .Where(s => s.Creator.UserId == userId)
          .Select(s => new SeriesListItemDto
          {
            SeriesId = s.SeriesId,
            Title = s.Title,
            LastChapterNumber = s.Chapters
                  .OrderByDescending(c => c.ChapterNumber)
                  .Select(c => (float?)c.ChapterNumber)
                  .FirstOrDefault() ?? 0f
          })
          .OrderBy(s => s.Title)
          .ToListAsync(cancellationToken);
    }

    public async Task<PaginatedList<SeriesDto>> GetSeriesListAsync(string sortBy = "newest", int page = 1, int pageSize = 20)
    {
      var query = _context.Series
          .Include(s => s.Creator)
          .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)
          .Include(s => s.Chapters).ThenInclude(c => c.Team)
          .Where(s => s.Status == SeriesStatus.ONGOING || s.Status == SeriesStatus.COMPLETED)
          .AsQueryable();

      if (sortBy.Equals("popular", StringComparison.OrdinalIgnoreCase))
        query = query.OrderByDescending(s => s.TotalRatings);
      else
        query = query.OrderByDescending(s => s.CreatedAt);

      var totalCount = await query.CountAsync();
      var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

      return new PaginatedList<SeriesDto>
      {
        TotalCount = totalCount,
        Page = page,
        PageSize = pageSize,
        Items = items.Select(MapToDto).ToList()
      };
    }

    public async Task<PaginatedList<SeriesDto>> SearchSeriesAsync(SeriesSearchRequest request)
    {
      var query = _context.Series
          .Include(s => s.Creator)
          .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)
          .Include(s => s.Chapters).ThenInclude(c => c.Team)
          .AsQueryable();

      if (!string.IsNullOrWhiteSpace(request.Keyword))
      {
        query = query.Where(s => s.Title.Contains(request.Keyword) ||
                                 (s.Description != null && s.Description.Contains(request.Keyword)));
      }
      if (request.GenreId.HasValue)
        query = query.Where(s => s.SeriesGenres.Any(sg => sg.GenreId == request.GenreId.Value));

      if (request.Status.HasValue)
        query = query.Where(s => s.Status == request.Status.Value);

      if (request.Format.HasValue)
        query = query.Where(s => s.SeriesFormat == request.Format.Value);

      if (request.SortBy.Equals("popular", StringComparison.OrdinalIgnoreCase))
        query = query.OrderByDescending(s => s.TotalRatings);
      else
        query = query.OrderByDescending(s => s.CreatedAt);

      var totalCount = await query.CountAsync();
      var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync();

      return new PaginatedList<SeriesDto>
      {
        TotalCount = totalCount,
        Page = request.Page,
        PageSize = request.PageSize,
        Items = items.Select(MapToDto).ToList()
      };
    }

    public async Task<SeriesDetailDto?> GetSeriesDetailsAsync(int seriesId)
    {
      var series = await _context.Series
          .Include(s => s.Creator)
          .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)
          .Include(s => s.Chapters)
          .FirstOrDefaultAsync(s => s.SeriesId == seriesId);

      if (series == null) return null;

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
        CreatedAt = series.CreatedAt,
        CreatorId = series.CreatorId,
        CreatorName = series.Creator.PenName,
        Genres = series.SeriesGenres.Select(sg => sg.Genre.Name).ToList(),
        Chapters = series.Chapters.OrderByDescending(c => c.PublishedAt).Select(c => new SeriesChapterDto
        {
          ChapterId = c.ChapterId,
          Title = c.Title ?? "Untitled",
          ChapterNumber = (int)c.ChapterNumber,
          Price = c.UnlockPriceCoins ?? 0,
          PublishedAt = c.PublishedAt ?? DateTime.UtcNow,
          ViewCount = c.ReadingHistories?.Count ?? 0
        }).ToList()
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
        GenreIds = series.SeriesGenres.Select(sg => sg.GenreId).ToList(),
        Violence = series.ViolenceScore,
        Nudity = series.NudityScore, 
        SexualContent = series.SexualScore,
        LanguageScore = series.LanguageScore,
        Substances = series.SubstancesScore,
        SensitiveContent = series.SensitiveScore,
        AgeRating = series.AgeRating
      };
    }

    public async Task<List<SeriesDto>> GetRecommendationsAsync(int userId, int limit = 10)
    {
      var randomSeries = await _context.Series
          .Include(s => s.Creator)
          .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)
          .Where(s => s.AverageRating >= 4.0m)
          .OrderBy(r => Guid.NewGuid())
          .Take(limit)
          .ToListAsync();

      if (!randomSeries.Any())
      {
        randomSeries = await _context.Series
           .Include(s => s.Creator)
           .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)
           .OrderBy(r => Guid.NewGuid())
           .Take(limit)
           .ToListAsync();
      }

      return randomSeries.Select(MapToDto).ToList();
    }

    public async Task DeleteAsync(int seriesId, int userId, CancellationToken cancellationToken = default)
    {
      var series = await _context.Series
          .Include(s => s.Creator)
          .FirstOrDefaultAsync(s => s.SeriesId == seriesId, cancellationToken)
          ?? throw new KeyNotFoundException($"Series {seriesId} không tồn tại.");

      if (series.Creator.UserId != userId)
      {
        throw new UnauthorizedAccessException("Bạn không có quyền xóa bộ truyện này.");
      }

      if (!string.IsNullOrEmpty(series.CoverImageUrl))
      {
        await _storage.DeleteAsync(series.CoverImageUrl, cancellationToken);
      }

      _context.Series.Remove(series);
      await _context.SaveChangesAsync(cancellationToken);

      _logger.LogInformation("Xóa truyện thành công. SeriesId: {SeriesId}, UserId: {UserId}", seriesId, userId);
    }

    private SeriesDto MapToDto(Series s)
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
        CreatedAt = s.CreatedAt,
        CreatorId = s.CreatorId,
        CreatorName = s.Creator.PenName,
        Genres = s.SeriesGenres.Select(sg => sg.Genre.Name).ToList(),
        LatestChapters = s.Chapters
              .OrderByDescending(c => c.PublishedAt)
              .Take(2)
              .Select(c => new SeriesChapterDto
              {
                ChapterId = c.ChapterId,
                Title = c.Title ?? "Untitled",
                ChapterNumber = (int)c.ChapterNumber,
                Price = c.UnlockPriceCoins ?? 0,
                PublishedAt = c.PublishedAt ?? DateTime.UtcNow,
                ViewCount = c.ReadingHistories?.Count ?? 0,
                GroupName = c.Team?.TeamName
              }).ToList()
      };
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
        0           => AgeRating.ALL,
        <= 5        => AgeRating.TEEN,
        <= 11       => AgeRating.MATURE,
        _           => AgeRating.ADULT
      };
    }
  }
}
