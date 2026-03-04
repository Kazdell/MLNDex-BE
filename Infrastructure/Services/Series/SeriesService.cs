using Application.DTOs.Series;
using Application.Interfaces.Data;
using Application.Interfaces.Series;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Services.Series
{
    public class SeriesService : ISeriesService
    {
        private readonly IMlndexDbContext _db;

        public SeriesService(IMlndexDbContext db)
        {
            _db = db;
        }

        public async Task<PaginatedList<SeriesDto>> GetSeriesListAsync(string sortBy = "newest", int page = 1, int pageSize = 20)
        {
            var query = _db.Series
                .Include(s => s.Creator)
                .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)
                .Where(s => s.Status == SeriesStatus.ONGOING || s.Status == SeriesStatus.COMPLETED)
                .AsQueryable();

            if (sortBy.ToLower() == "popular")
            {
                query = query.OrderByDescending(s => s.TotalRatings);
            }
            else // newest
            {
                query = query.OrderByDescending(s => s.CreatedAt);
            }

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
            var query = _db.Series
                .Include(s => s.Creator)
                .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                query = query.Where(s => s.Title.Contains(request.Keyword) || 
                                         (s.Description != null && s.Description.Contains(request.Keyword)));
            }
            if (request.GenreId.HasValue)
            {
                query = query.Where(s => s.SeriesGenres.Any(sg => sg.GenreId == request.GenreId.Value));
            }
            if (request.Status.HasValue)
            {
                query = query.Where(s => s.Status == request.Status.Value);
            }
            if (request.Format.HasValue)
            {
                query = query.Where(s => s.SeriesFormat == request.Format.Value);
            }

            if (request.SortBy.ToLower() == "popular")
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
            var series = await _db.Series
                .Include(s => s.Creator)
                .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)
                .Include(s => s.Chapters)
                .FirstOrDefaultAsync(s => s.SeriesId == seriesId);

            if (series == null) return null;

            var dto = new SeriesDetailDto
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

            return dto;
        }

        public async Task<List<SeriesDto>> GetRecommendationsAsync(int userId, int limit = 10)
        {
            // Simple logic for now: Random high rated
            var randomSeries = await _db.Series
                .Include(s => s.Creator)
                .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)
                .Where(s => s.AverageRating >= 4.0m)
                .OrderBy(r => Guid.NewGuid())
                .Take(limit)
                .ToListAsync();

            // Fallback to random if no high rated
            if (!randomSeries.Any())
            {
                 randomSeries = await _db.Series
                    .Include(s => s.Creator)
                    .Include(s => s.SeriesGenres).ThenInclude(sg => sg.Genre)
                    .OrderBy(r => Guid.NewGuid())
                    .Take(limit)
                    .ToListAsync();
            }

            return randomSeries.Select(MapToDto).ToList();
        }

        private SeriesDto MapToDto(Domain.Entities.Series s)
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
                Genres = s.SeriesGenres.Select(sg => sg.Genre.Name).ToList()
            };
        }
    }
}
