using Application.DTOs.Creator;
using Application.Interfaces;
using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services.Creator
{
  public class SeriesService : ISeriesService
  {
    private readonly IMlndexDbContext _context;
    private readonly IStorageService _storage;
    private readonly ILogger<SeriesService> _logger;

    public SeriesService(
        IMlndexDbContext context,
        IStorageService storage,
        ILogger<SeriesService> logger)
    {
      _context = context;
      _storage = storage;
      _logger = logger;
    }

    public async Task<CreateSeriesResponseDto> CreateAsync(
        int creatorId,
        CreateSeriesDto dto,
        CancellationToken cancellationToken = default)
    {
      var creator = await _context.CreatorProfiles
          .FindAsync([creatorId], cancellationToken)
          ?? throw new KeyNotFoundException($"Creator {creatorId} không tồn tại.");

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
        var maxScore = new[] { dto.Violence, dto.Nudity, dto.SexualContent }.Max();
        var ageRating = maxScore switch
        {
          >= 3 => AgeRating.ADULT,
          >= 2 => AgeRating.MATURE,
          >= 1 => AgeRating.TEEN,
          _ => AgeRating.ALL
        };

        var series = new Series
        {
          CreatorId = creatorId,
          Title = dto.Title,
          Description = dto.Description,
          CoverImageUrl = imageUrl,
          SeriesFormat = SeriesFormat.NOVEL,
          AgeRating = ageRating,
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

        _logger.LogInformation(
            "Tạo novel thành công. SeriesId: {SeriesId}, Title: {Title}, CreatorId: {CreatorId}.",
            series.SeriesId, series.Title, creatorId);

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
        int creatorId,
        CreateSeriesDto dto,
        CancellationToken cancellationToken = default)
    {
      var series = await _context.Series
          .Include(s => s.SeriesGenres)
          .FirstOrDefaultAsync(s => s.SeriesId == seriesId, cancellationToken)
          ?? throw new KeyNotFoundException($"Series {seriesId} không tồn tại.");

      if (series.CreatorId != creatorId)
      {
        throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa bộ truyện này.");
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
        var maxScore = new[] { dto.Violence, dto.Nudity, dto.SexualContent }.Max();
        series.AgeRating = maxScore switch
        {
          >= 3 => AgeRating.ADULT,
          >= 2 => AgeRating.MATURE,
          >= 1 => AgeRating.TEEN,
          _ => AgeRating.ALL
        };

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
        int creatorId,
        CancellationToken cancellationToken = default)
    {
      return await _context.Series
          .Where(s => s.CreatorId == creatorId)
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

    public async Task<CreateSeriesDto?> GetForEditAsync(int seriesId, int creatorId)
    {
      var series = await _context.Series
          .Include(s => s.SeriesGenres)
          .FirstOrDefaultAsync(s => s.SeriesId == seriesId);

      if (series == null || series.CreatorId != creatorId) return null;

      return new CreateSeriesDto
      {
        Title = series.Title,
        Description = series.Description,
        GenreIds = series.SeriesGenres.Select(sg => sg.GenreId).ToList(),
        Violence = (int)series.AgeRating, // Fallback mapping
        Nudity = 0,
        SexualContent = 0
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
  }
}
