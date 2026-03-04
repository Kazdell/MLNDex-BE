using Domain.Entities;
using System;
using System.Collections.Generic;

namespace Application.DTOs.Series
{
    public class SeriesDto
    {
        public int SeriesId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public string SeriesFormat { get; set; } = null!;
        public string AgeRating { get; set; } = null!;
        public string Status { get; set; } = null!;
        public decimal AverageRating { get; set; }
        public int TotalRatings { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Creator info (Simplified)
        public int CreatorId { get; set; }
        public string CreatorName { get; set; } = null!;

        // Genres
        public List<string> Genres { get; set; } = new List<string>();
    }

    public class SeriesDetailDto : SeriesDto
    {
        public List<SeriesChapterDto> Chapters { get; set; } = new List<SeriesChapterDto>();
    }

    public class SeriesChapterDto
    {
        public int ChapterId { get; set; }
        public string Title { get; set; } = null!;
        public int? ChapterNumber { get; set; }
        public int? VolumeNumber { get; set; }
        public int Price { get; set; }
        public DateTime PublishedAt { get; set; }
        public int ViewCount { get; set; }
    }

    public class SeriesSearchRequest
    {
        public string? Keyword { get; set; }
        public int? GenreId { get; set; }
        public SeriesStatus? Status { get; set; }
        public SeriesFormat? Format { get; set; }
        public string SortBy { get; set; } = "newest"; // newest, popular
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class PaginatedList<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
