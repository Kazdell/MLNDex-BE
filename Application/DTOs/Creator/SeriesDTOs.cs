using Domain.Entities;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Creator
{
  public class CreateSeriesDto
  {
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string Language { get; set; } = null!;
    public List<int> GenreIds { get; set; } = new List<int>();
    public IFormFile? CoverImage { get; set; }
    public string? CoverImageUrl { get; set; }
    // Moderation fields
    public int Violence { get; set; }
    public int Nudity { get; set; }
    public int SexualContent { get; set; }
    public int LanguageScore { get; set; }
    public int Substances { get; set; }
    public int SensitiveContent { get; set; }
    public AgeRating? AgeRating { get; set; }
    public string? ModerationStatus { get; set; }
    // Series status (On-going, Hiatus, Completed, Dropped)
    public string? Status { get; set; }
  }

  public class CreateSeriesResponseDto
  {
    public int SeriesId { get; set; }
    public string Title { get; set; } = null!;
    public string? CoverImageUrl { get; set; }
    public string AgeRating { get; set; } = null!;
    public string ModerationStatus { get; set; } = null!;
  }

  public class SeriesListItemDto
  {
    public int SeriesId { get; set; }
    public string Title { get; set; } = null!;
    public float LastChapterNumber { get; set; }

    // ── Thêm mới ──────────────────────────────────────────────
    public string? CoverImageUrl { get; set; }
    public string Status { get; set; } = "On-going";        // On-going | Hiatus | Completed | Dropped
    public string ModerationStatus { get; set; } = null!;   // APPROVED | PENDING | REJECTED
    public string AgeRating { get; set; } = null!;
    public int ChapterCount { get; set; }
    public long TotalViews { get; set; }
    public DateTime? LastUpdatedAt { get; set; }            // Ngày upload chapter mới nhất
    public List<string> Genres { get; set; } = new();
  }

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
    public int CreatorUserId { get; set; }
    public string CreatorName { get; set; } = null!;

    // Genres
    public List<string> Genres { get; set; } = new List<string>();

    // Latest Chapters for Landing Page
    public List<SeriesChapterDto> LatestChapters { get; set; } = new List<SeriesChapterDto>();
  }

  public class SeriesDetailDto : SeriesDto
  {
    public List<SeriesChapterDto> Chapters { get; set; } = new List<SeriesChapterDto>();
    public string? OriginalLanguage { get; set; }  // Language code of the series (e.g. "vi")
  }

  public class SeriesChapterDto
  {
    public int ChapterId { get; set; }
    public string Title { get; set; } = null!;
    public int? ChapterNumber { get; set; }
    public int Price { get; set; }
    public DateTime PublishedAt { get; set; }
    public int ViewCount { get; set; }
    public string? GroupName { get; set; }
    public int? TeamId { get; set; }
    public bool IsOriginal { get; set; }        // true if TeamId == null (author upload)
    public string? LanguageCode { get; set; }   // "vi", "ja", "en"...
    public string? LanguageName { get; set; }    // "Tiếng Việt", "日本語"...
    public string? UploaderName { get; set; }    // Uploader display name
    public int CommentCount { get; set; }
    public int PageCount { get; set; }
    public bool IsOfficialTranslation { get; set; } // If it's a translation and has official permission
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
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
    public decimal? MinRating { get; set; }
    public int? CreatorId { get; set; }
    public int? ExcludeSeriesId { get; set; }
  }

  public class PaginatedList<T>
  {
    public List<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
  }

  public class UpdateSeriesStatusRequest
  {
    public string Status { get; set; } = null!;
  }
}
