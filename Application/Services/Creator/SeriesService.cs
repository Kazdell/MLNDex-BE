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

            // 1. Rate Limit: Max 5 series/ngày
            var todayUtc = DateTime.UtcNow.Date;
            var seriesToday = await _context.Series
                .Where(s => s.Creator.UserId == userId && s.CreatedAt >= todayUtc)
                .CountAsync(cancellationToken);
            if (seriesToday >= 5)
                throw new InvalidOperationException(
                    "Bạn đã đạt giới hạn 5 bộ truyện/ngày. Vui lòng quay lại ngày mai.");

            // 2. Check Title Duplicate
            var titleExists = await _context.Series.AnyAsync(s => s.Title.ToLower() == dto.Title.ToLower(), cancellationToken);
            if (titleExists)
                throw new ArgumentException("Tiêu đề truyện này đã tồn tại trên hệ thống.");

            // 1b. Check Description Duplicate
            if (!string.IsNullOrEmpty(dto.Description))
            {
                var descExists = await _context.Series.AnyAsync(s => s.Description == dto.Description, cancellationToken);
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
                ?? throw new KeyNotFoundException($"Series {seriesId} không tồn tại.");

            if (series.Creator.UserId != userId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa bộ truyện này.");
            }

            // ── Update Cooldown: 10 phút giữa 2 lần sửa ────────────────────
            if (series.UpdatedAt.HasValue
                && (DateTime.UtcNow - series.UpdatedAt.Value).TotalMinutes < 10)
            {
                var remaining = 10 - (DateTime.UtcNow - series.UpdatedAt.Value).TotalMinutes;
                throw new InvalidOperationException(
                    $"Vui lòng đợi {Math.Ceiling(remaining)} phút trước khi chỉnh sửa lại.");
            }

            if (await _context.Series.AnyAsync(s => s.Title.ToLower() == dto.Title.ToLower() && s.SeriesId != seriesId, cancellationToken))
                throw new ArgumentException("Tiêu đề mới đã tồn tại.");

            if (!string.IsNullOrEmpty(dto.Description))
            {
                if (await _context.Series.AnyAsync(s => s.Description == dto.Description && s.SeriesId != seriesId, cancellationToken))
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
                ?? throw new KeyNotFoundException($"Creator với UserId {userId} không tồn tại.");

            var series = await _context.Series
                .Where(s => s.CreatorId == creator.CreatorId)
                .Include(s => s.Chapters)
                .ThenInclude(c => c.ReadingHistories)
                .Include(s => s.SeriesGenres)
                .ThenInclude(sg => sg.Genre)
                .OrderByDescending(s => s.CreatedAt)
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

                TotalViews = s.Chapters.Sum(c => (long)(c.ReadingHistories?.Count ?? 0)),

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
            var query = _context.Series
                .Where(s => s.Status == SeriesStatus.ONGOING || s.Status == SeriesStatus.COMPLETED)
                .AsQueryable();

            if (sortBy.Equals("popular", StringComparison.OrdinalIgnoreCase))
                query = query.OrderByDescending(s => s.TotalRatings);
            else if (sortBy.Equals("newest", StringComparison.OrdinalIgnoreCase))
                query = query.OrderByDescending(s => s.Chapters.Any() ? s.Chapters.OrderByDescending(c => c.ChapterNumber).FirstOrDefault().PublishedAt : s.CreatedAt);
            else
                query = query.OrderByDescending(s => s.CreatedAt);

            var totalCount = await query.CountAsync();
            var seriesData = await query
                .Select(s => new { s.SeriesId })
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var ids = seriesData.Select(x => x.SeriesId).ToList();

            if (!ids.Any()) return new PaginatedList<SeriesDto> { TotalCount = totalCount, Page = page, PageSize = pageSize, Items = new List<SeriesDto>() };

            var items = await _context.Series
                .Include(s => s.Creator)
                .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)
                .Include(s => s.Chapters.Where(c => c.Status == ChapterStatus.PUBLISHED).OrderByDescending(c => c.ChapterNumber).Take(2)).ThenInclude(c => c.Team)
                .Include(s => s.Chapters.Where(c => c.Status == ChapterStatus.PUBLISHED).OrderByDescending(c => c.ChapterNumber).Take(2)).ThenInclude(c => c.Language)
                .Include(s => s.Chapters.Where(c => c.Status == ChapterStatus.PUBLISHED).OrderByDescending(c => c.ChapterNumber).Take(2)).ThenInclude(c => c.Translations)
                .Where(s => ids.Contains(s.SeriesId))
                .ToListAsync();

            var orderedItems = ids.Select(id => items.First(i => i.SeriesId == id)).ToList();

            return new PaginatedList<SeriesDto>
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                Items = orderedItems.Select(MapToDto).ToList()
            };
        }

        public async Task<PaginatedList<SeriesDto>> SearchSeriesAsync(SeriesSearchRequest request)
        {
            var query = _context.Series
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
                query = query.OrderByDescending(s => s.TotalRatings);
            else if (string.Equals(request.SortBy, "newest", StringComparison.OrdinalIgnoreCase))
                query = query.OrderByDescending(s => s.Chapters.Any() ? s.Chapters.OrderByDescending(c => c.ChapterNumber).FirstOrDefault().PublishedAt : s.CreatedAt);
            else
                query = query.OrderByDescending(s => s.CreatedAt);

            var totalCount = await query.CountAsync();
            var seriesData = await query
                .Select(s => new { s.SeriesId })
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            var ids = seriesData.Select(x => x.SeriesId).ToList();

            if (!ids.Any()) return new PaginatedList<SeriesDto> { TotalCount = totalCount, Page = request.Page, PageSize = request.PageSize, Items = new List<SeriesDto>() };

            var items = await _context.Series
                .Include(s => s.Creator)
                .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)
                .Include(s => s.Chapters.Where(c => c.Status == ChapterStatus.PUBLISHED).OrderByDescending(c => c.ChapterNumber).Take(2)).ThenInclude(c => c.Team)
                .Include(s => s.Chapters.Where(c => c.Status == ChapterStatus.PUBLISHED).OrderByDescending(c => c.ChapterNumber).Take(2)).ThenInclude(c => c.Language)
                .Include(s => s.Chapters.Where(c => c.Status == ChapterStatus.PUBLISHED).OrderByDescending(c => c.ChapterNumber).Take(2)).ThenInclude(c => c.Translations)
                .Where(s => ids.Contains(s.SeriesId))
                .ToListAsync();

            var orderedItems = ids.Select(id => items.First(i => i.SeriesId == id)).ToList();

            return new PaginatedList<SeriesDto>
            {
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize,
                Items = orderedItems.Select(MapToDto).ToList()
            };
        }

        public async Task<SeriesDetailDto?> GetSeriesDetailsAsync(int seriesId)
        {
            var series = await _context.Series
                .Include(s => s.Creator)
                .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)
                .Include(s => s.Chapters).ThenInclude(c => c.Team)
                .Include(s => s.Chapters).ThenInclude(c => c.Language)
                .Include(s => s.Chapters).ThenInclude(c => c.Translations)
                .FirstOrDefaultAsync(s => s.SeriesId == seriesId);

            if (series == null) return null;

            // Detect original language from the first original chapter (TeamId == null)
            var firstOriginalChapter = series.Chapters
                .Where(c => c.TeamId == null && c.Language != null)
                .OrderBy(c => c.ChapterNumber)
                .FirstOrDefault();

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
                CreatorUserId = series.Creator.UserId,
                CreatorName = series.Creator.PenName,
                Genres = series.SeriesGenres.Select(sg => sg.Genre.Name).ToList(),
                OriginalLanguage = firstOriginalChapter?.Language?.Code,
                Chapters = series.Chapters
                    .Where(c => c.Status == ChapterStatus.PUBLISHED)
                    .OrderByDescending(c => c.ChapterNumber)
                    .Select(c => new SeriesChapterDto
                    {
                        ChapterId = c.ChapterId,
                        Title = c.Title ?? "Untitled",
                        ChapterNumber = (int)c.ChapterNumber,
                        Price = c.UnlockPriceCoins ?? 0,
                        PublishedAt = c.PublishedAt ?? DateTime.UtcNow,
                        ViewCount = c.ReadingHistories?.Count ?? 0,
                        GroupName = c.Team?.TeamName,
                        TeamId = c.TeamId,
                        IsOriginal = c.TeamId == null,
                        IsOfficialTranslation = c.Translations.Any(t => t.IsOfficial),
                        LanguageCode = c.Language?.Code,
                        LanguageName = c.Language?.Name,
                        CommentCount = 0
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

        public async Task<List<SeriesDto>> GetRecommendationsAsync(int userId, int limit = 10, int? currentSeriesId = null)
        {
            var recommendedSeriesIds = new HashSet<int>();
            var excludedIds = new HashSet<int>();

            if (currentSeriesId.HasValue)
                excludedIds.Add(currentSeriesId.Value);

            // Fetch user reading history and preferences if logged in
            var readSeriesIds = new List<int>();
            var userPreferredGenreIds = new List<int>();

            if (userId > 0)
            {
                readSeriesIds = await _context.ReadingHistories
                    .Where(rh => rh.UserId == userId)
                    .Select(rh => rh.SeriesId)
                    .Distinct()
                    .ToListAsync();

                excludedIds.UnionWith(readSeriesIds);

                userPreferredGenreIds = await _context.ReadingHistories
                    .Where(rh => rh.UserId == userId)
                    .SelectMany(rh => rh.Series.SeriesGenres.Select(sg => sg.GenreId))
                    .GroupBy(g => g)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .Take(5) // Top 5 preferred genres
                    .ToListAsync();
            }

            // TIER 2: Warm Start / Context-Aware (If currently reading a series) //
            if (currentSeriesId.HasValue)
            {
                // Item-Based CF pseudo: Users who read this also read...
                var coReaders = await _context.ReadingHistories
                    .Where(rh => rh.SeriesId == currentSeriesId.Value)
                    .Select(rh => rh.UserId)
                    .Take(100)
                    .ToListAsync();

                if (coReaders.Any())
                {
                    var coReadSeriesIds = await _context.ReadingHistories
                        .Where(rh => coReaders.Contains(rh.UserId) && rh.SeriesId != currentSeriesId.Value)
                        .GroupBy(rh => rh.SeriesId)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .Take(limit / 2)
                        .ToListAsync();

                    recommendedSeriesIds.UnionWith(coReadSeriesIds.Where(id => !excludedIds.Contains(id)));
                }

                // If not enough from co-reads, try same creator or genre
                if (recommendedSeriesIds.Count < limit / 2)
                {
                   var currentSeries = await _context.Series
                       .Include(s => s.SeriesGenres)
                       .FirstOrDefaultAsync(s => s.SeriesId == currentSeriesId.Value);
                   
                   if (currentSeries != null)
                   {
                       var currentGenres = currentSeries.SeriesGenres.Select(sg => sg.GenreId).ToList();
                       
                       var contextSeriesList = await _context.Series
                           .Where(s => (s.Status == SeriesStatus.ONGOING || s.Status == SeriesStatus.COMPLETED)
                                       && !excludedIds.Contains(s.SeriesId) 
                                       && !recommendedSeriesIds.Contains(s.SeriesId)
                                       && (s.CreatorId == currentSeries.CreatorId || s.SeriesGenres.Any(sg => currentGenres.Contains(sg.GenreId))))
                           .OrderByDescending(s => s.ReadingHistories.Count)
                           .Select(s => s.SeriesId)
                           .Take(limit / 2)
                           .ToListAsync();
                           
                       recommendedSeriesIds.UnionWith(contextSeriesList);
                   }
                }
            }

            // TIER 3: Deep Personalization (If user has enough history) //
            if (userId > 0 && readSeriesIds.Count >= 5 && recommendedSeriesIds.Count < limit)
            {
                int remainingLimit = limit - recommendedSeriesIds.Count;
                int cfLimit = (int)(remainingLimit * 0.7); // 70% from CF
                
                // 1. Collaborative Filtering (User-Based)
                // Find top 50 users who also read the same series
                var similarUsers = await _context.ReadingHistories
                    .Where(rh => readSeriesIds.Contains(rh.SeriesId) && rh.UserId != userId)
                    .GroupBy(rh => rh.UserId)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .Take(50)
                    .ToListAsync();
                    
                if (similarUsers.Any())
                {
                    // Find top series read by these similar users, excluding already read & recommended
                    var cfRecommendedIds = await _context.ReadingHistories
                        .Where(rh => similarUsers.Contains(rh.UserId) 
                                  && !excludedIds.Contains(rh.SeriesId)
                                  && !recommendedSeriesIds.Contains(rh.SeriesId))
                        .GroupBy(rh => rh.SeriesId)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .Take(cfLimit)
                        .ToListAsync();

                    recommendedSeriesIds.UnionWith(cfRecommendedIds);
                }
                
                // 2. Content-Based Filtering (Top Genres)
                if (recommendedSeriesIds.Count < limit && userPreferredGenreIds.Any())
                {
                    remainingLimit = limit - recommendedSeriesIds.Count;
                    var personalizedIds = await _context.Series
                        .Where(s => (s.Status == SeriesStatus.ONGOING || s.Status == SeriesStatus.COMPLETED)
                                    && !excludedIds.Contains(s.SeriesId)
                                    && !recommendedSeriesIds.Contains(s.SeriesId)
                                    && s.SeriesGenres.Any(sg => userPreferredGenreIds.Contains(sg.GenreId)))
                        .OrderByDescending(s => s.TotalRatings) 
                        .Select(s => s.SeriesId)
                        .Take(remainingLimit)
                        .ToListAsync();

                    recommendedSeriesIds.UnionWith(personalizedIds);
                }
            }

            // TIER 1: Cold Start / Fallback & Diversity Injection //
            if (recommendedSeriesIds.Count < limit)
            {
                var remainingLimit = limit - recommendedSeriesIds.Count;
                
                // Trending / High rating series
                var popularIds = await _context.Series
                    .Where(s => (s.Status == SeriesStatus.ONGOING || s.Status == SeriesStatus.COMPLETED)
                                && !excludedIds.Contains(s.SeriesId)
                                && !recommendedSeriesIds.Contains(s.SeriesId))
                    .OrderByDescending(s => s.TotalRatings) // Changed back to TotalRatings since TotalViews doesn't exist on Series directly
                    .ThenByDescending(s => s.AverageRating)
                    .Select(s => s.SeriesId)
                    .Take(remainingLimit)
                    .ToListAsync();

                recommendedSeriesIds.UnionWith(popularIds);
            }

            // Fetch actual full series data for the selected IDs
            var finalRecIds = recommendedSeriesIds.Take(limit).ToList();
            if (!finalRecIds.Any()) return new List<SeriesDto>();

            var recItems = await _context.Series
                .Include(s => s.Creator)
                .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)
                .Include(s => s.Chapters.Where(c => c.Status == ChapterStatus.PUBLISHED).OrderByDescending(c => c.ChapterNumber).Take(2)).ThenInclude(c => c.Team)
                .Include(s => s.Chapters.Where(c => c.Status == ChapterStatus.PUBLISHED).OrderByDescending(c => c.ChapterNumber).Take(2)).ThenInclude(c => c.Language)
                .Include(s => s.Chapters.Where(c => c.Status == ChapterStatus.PUBLISHED).OrderByDescending(c => c.ChapterNumber).Take(2)).ThenInclude(c => c.Translations)
                .Where(s => finalRecIds.Contains(s.SeriesId))
                .ToListAsync();

            // Maintain order returned by algorithm
            var orderedRecs = finalRecIds.Select(id => recItems.First(i => i.SeriesId == id)).ToList();

            return orderedRecs.Select(MapToDto).ToList();
        }

        // ── Status-only update (no moderation, no cooldown) ──────────────────
        public async Task UpdateStatusAsync(
            int seriesId, int userId, string status,
            CancellationToken cancellationToken = default)
        {
            var series = await _context.Series
                .Include(s => s.Creator)
                .FirstOrDefaultAsync(s => s.SeriesId == seriesId, cancellationToken)
                ?? throw new KeyNotFoundException($"Series {seriesId} không tồn tại.");

            if (series.Creator.UserId != userId)
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa bộ truyện này.");

            if (!Enum.TryParse<SeriesStatus>(status, true, out var newStatus))
                throw new ArgumentException($"Trạng thái '{status}' không hợp lệ. Chấp nhận: ONGOING, COMPLETED, HALTED, DROPPED.");

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
                ?? throw new KeyNotFoundException($"Series {seriesId} không tồn tại.");

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
                throw new InvalidOperationException(
                    "Không thể xóa truyện do dữ liệu liên quan. Vui lòng thử lại sau.", ex);
            }
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
                CreatorUserId = s.Creator.UserId,
                CreatorName = s.Creator.PenName,
                Genres = s.SeriesGenres.Select(sg => sg.Genre.Name).ToList(),
                LatestChapters = s.Chapters
                    .Where(c => c.Status == ChapterStatus.PUBLISHED)
                    .OrderByDescending(c => c.ChapterNumber)
                    .Take(2)
                    .Select(c => new SeriesChapterDto
                    {
                        ChapterId = c.ChapterId,
                        Title = c.Title ?? "Untitled",
                        ChapterNumber = (int)c.ChapterNumber,
                        Price = c.UnlockPriceCoins ?? 0,
                        PublishedAt = c.PublishedAt ?? DateTime.UtcNow,
                        ViewCount = c.ReadingHistories?.Count ?? 0,
                        GroupName = c.Team?.TeamName,
                        TeamId = c.TeamId,
                        IsOriginal = c.TeamId == null,
                        IsOfficialTranslation = c.Translations != null && c.Translations.Any(t => t.IsOfficial),
                        LanguageCode = c.Language?.Code,
                        LanguageName = c.Language?.Name,
                        CommentCount = 0
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
                0 => AgeRating.ALL,
                <= 5 => AgeRating.TEEN,
                <= 11 => AgeRating.MATURE,
                _ => AgeRating.ADULT
            };
        }
    }
}
