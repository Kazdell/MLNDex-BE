using Application.DTOs.Creator;
using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Application.Services.Creator
{
  public class RecommendationService : IRecommendationService
  {
    private readonly IMlndexDbContext _context;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        IMlndexDbContext context,
        ILogger<RecommendationService> logger)
    {
      _context = context;
      _logger = logger;
    }

    public async Task<List<SeriesDto>> GetRecommendationsAsync(int userId, int limit = 10, int? currentSeriesId = null)
    {
      var recommendedSeriesIds = new HashSet<int>();
      var excludedIds = new HashSet<int>();

      if (currentSeriesId.HasValue)
        excludedIds.Add(currentSeriesId.Value);

      // Fetch user reading history and preferences if logged in
      var readSeriesIds = new List<int>();

      if (userId > 0)
      {
        readSeriesIds = await _context.ReadingHistories
            .Where(rh => rh.UserId == userId)
            .Select(rh => rh.SeriesId)
            .Distinct()
            .ToListAsync();

        excludedIds.UnionWith(readSeriesIds);
      }

      // ==========================================
      // TIER 2: WARM START (CONTEXT-AWARE)
      // ==========================================
      if (currentSeriesId.HasValue)
      {
        // Item-Based CF: Users who read this also read...
        var coReaders = await _context.ReadingHistories
            .Where(rh => rh.SeriesId == currentSeriesId.Value && rh.UserId != userId)
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
                .Take((limit / 2) - recommendedSeriesIds.Count)
                .ToListAsync();

            recommendedSeriesIds.UnionWith(contextSeriesList);
          }
        }
      }

      // ==========================================
      // TIER 3: DEEP PERSONALIZATION ALGORITHM
      // ==========================================
      if (userId <= 0)
      {
        // 1. User Not Logged In -> Top 10 Rating / Views
        await FillWithPopular(limit, recommendedSeriesIds, excludedIds);
        return await FetchAndMap(recommendedSeriesIds.Take(limit).ToList());
      }

      // User Logged In
      var todayUtc = DateTime.UtcNow;
      var monthDate = todayUtc.AddDays(-30);

      // 2. Lịch sử duyệt trong tháng
      var monthGenres = await _context.ReadingHistories
          .Where(rh => rh.UserId == userId && rh.LastReadAt >= monthDate)
          .SelectMany(rh => rh.Series.SeriesGenres.Select(sg => sg.GenreId))
          .GroupBy(g => g)
          .OrderByDescending(g => g.Count())
          .Select(g => g.Key)
          .Take(5)
          .ToListAsync();

      var totalGenres = await _context.ReadingHistories
          .Where(rh => rh.UserId == userId)
          .SelectMany(rh => rh.Series.SeriesGenres.Select(sg => sg.GenreId))
          .GroupBy(g => g)
          .OrderByDescending(g => g.Count())
          .Select(g => g.Key)
          .Take(5)
          .ToListAsync();

      var targetGenres = monthGenres.Any() ? monthGenres : totalGenres;

      // 3. Nếu không có lịch sử
      if (!targetGenres.Any())
      {
        await FillWithPopular(limit, recommendedSeriesIds, excludedIds);
        return await FetchAndMap(recommendedSeriesIds.Take(limit).ToList());
      }

      // 4. Vòng lặp CF cơ sở Genre (tối đa 3 vòng)
      int loopCount = 0;
      foreach (var genreId in targetGenres)
      {
        if (recommendedSeriesIds.Count >= limit) break;
        if (loopCount >= 3) break;

        await CollectCFByGenre(genreId, limit, recommendedSeriesIds, excludedIds, userId);
        loopCount++;
      }

      if (recommendedSeriesIds.Count >= limit)
        return await FetchAndMap(recommendedSeriesIds.Take(limit).ToList());

      // 5. Mở rộng Series Authors (Tìm thêm từ tác giả đã đọc)
      if (readSeriesIds.Any())
      {
        await CollectSameAuthor(limit, recommendedSeriesIds, excludedIds, readSeriesIds);
      }

      // KHÔNG CO FALLBACK NẾU CHƯA ĐỦ TRUYỆN. Trả về bao nhiêu có bấy nhiêu.
      return await FetchAndMap(recommendedSeriesIds.Take(limit).ToList());
    }

    private async Task CollectCFByGenre(int genreId, int limit, HashSet<int> recommendedSeriesIds, HashSet<int> excludedIds, int userId)
    {
      var remaining = limit - recommendedSeriesIds.Count;
      if (remaining <= 0) return;

      // Co-readers who read this genre
      var coReaders = await _context.ReadingHistories
          .Where(rh => rh.Series.SeriesGenres.Any(sg => sg.GenreId == genreId) && rh.UserId != userId)
          .Select(rh => rh.UserId)
          .Distinct()
          .Take(100) // "what other 100 people also read base on genre"
          .ToListAsync();

      if (coReaders.Any())
      {
        var cfList = await _context.ReadingHistories
             .Where(rh => coReaders.Contains(rh.UserId)
                 && (rh.Series.Status == SeriesStatus.ONGOING || rh.Series.Status == SeriesStatus.COMPLETED)
                 && !excludedIds.Contains(rh.SeriesId)
                 && !recommendedSeriesIds.Contains(rh.SeriesId)
                 && rh.Series.SeriesGenres.Any(sg => sg.GenreId == genreId))
             .GroupBy(rh => rh.SeriesId)
             .OrderByDescending(g => g.Count())
             .Select(g => g.Key)
             .Take(remaining)
             .ToListAsync();

        recommendedSeriesIds.UnionWith(cfList);
      }
    }

    private async Task CollectSameAuthor(int limit, HashSet<int> recommendedSeriesIds, HashSet<int> excludedIds, List<int> readSeriesIds)
    {
      var remaining = limit - recommendedSeriesIds.Count;
      if (remaining <= 0) return;

      var creatorIds = await _context.Series
          .Where(s => readSeriesIds.Contains(s.SeriesId))
          .Select(s => s.CreatorId)
          .Distinct()
          .ToListAsync();

      if (creatorIds.Any())
      {
        var authorSeries = await _context.Series
            .Where(s => creatorIds.Contains(s.CreatorId)
                && (s.Status == SeriesStatus.ONGOING || s.Status == SeriesStatus.COMPLETED)
                && !excludedIds.Contains(s.SeriesId)
                && !recommendedSeriesIds.Contains(s.SeriesId))
            .OrderByDescending(s => s.TotalRatings)
            .Select(s => s.SeriesId)
            .Take(remaining)
            .ToListAsync();

        recommendedSeriesIds.UnionWith(authorSeries);
      }
    }

    private async Task FillWithPopular(int limit, HashSet<int> recommendedSeriesIds, HashSet<int> excludedIds)
    {
      var remaining = limit - recommendedSeriesIds.Count;
      if (remaining <= 0) return;

      var popularIds = await _context.Series
          .Where(s => (s.Status == SeriesStatus.ONGOING || s.Status == SeriesStatus.COMPLETED)
                      && !excludedIds.Contains(s.SeriesId)
                      && !recommendedSeriesIds.Contains(s.SeriesId))
          .OrderByDescending(s => s.TotalRatings)
          .ThenByDescending(s => s.AverageRating)
          .Select(s => s.SeriesId)
          .Take(remaining)
          .ToListAsync();

      recommendedSeriesIds.UnionWith(popularIds);
    }

    private async Task<List<SeriesDto>> FetchAndMap(List<int> finalRecIds)
    {
      if (!finalRecIds.Any()) return new List<SeriesDto>();

      var recItems = await _context.Series
          .Include(s => s.Creator)
          .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)
          .Include(s => s.Chapters.Where(c => c.Status == ChapterStatus.PUBLISHED).OrderByDescending(c => c.ChapterNumber).Take(2)).ThenInclude(c => c.Team)
          .Include(s => s.Chapters.Where(c => c.Status == ChapterStatus.PUBLISHED).OrderByDescending(c => c.ChapterNumber).Take(2)).ThenInclude(c => c.Language)
          .Include(s => s.Chapters.Where(c => c.Status == ChapterStatus.PUBLISHED).OrderByDescending(c => c.ChapterNumber).Take(2)).ThenInclude(c => c.Translations)
          .Where(s => finalRecIds.Contains(s.SeriesId))
          .ToListAsync();

      var grantedPermsDict = await _context.TranslationPermissions
          .Where(p => finalRecIds.Contains(p.SeriesId) && p.Status == TranslationPermissionStatus.GRANTED)
          .GroupBy(p => p.SeriesId)
          .ToDictionaryAsync(g => g.Key, g => g.Select(p => p.TeamId).ToHashSet());

      var orderedRecs = finalRecIds.Select(id => recItems.FirstOrDefault(i => i.SeriesId == id)).Where(s => s != null).Select(s => s!).ToList();

      return orderedRecs.Select(s => MapToDto(s, grantedPermsDict.GetValueOrDefault(s.SeriesId))).ToList();
    }

    private SeriesDto MapToDto(Series s, HashSet<int>? grantedTeamIds = null)
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
                  IsOfficialTranslation = IsChapterOfficialTranslation(c, grantedTeamIds),
                  LanguageCode = c.Language?.Code,
                  LanguageName = c.Language?.Name,
                  CommentCount = 0
                }).ToList()
      };
    }

    private static bool IsChapterOfficialTranslation(Chapter c, HashSet<int>? grantedTeamIds)
    {
      if (c.Translations != null && c.Translations.Any(t => t.IsOfficial))
        return true;

      if (c.TeamId != null && grantedTeamIds != null && grantedTeamIds.Contains(c.TeamId.Value))
        return true;

      return false;
    }
  }
}
