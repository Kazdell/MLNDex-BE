using System;
using System.Collections.Generic;

namespace Application.DTOs.Translation.Responses
{
  // Full translation details response
  public class TranslationResponse
  {
    public int TranslationId { get; set; }
    public int ChapterId { get; set; }
    public int? SeriesId { get; set; }
    public string? SeriesTitle { get; set; }
    public string? Title { get; set; }
    public float? ChapterNumber { get; set; }
    public int LanguageId { get; set; }
    public string LanguageName { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string QualityStatus { get; set; } = string.Empty;
    public string ModerationStatus { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public bool IsOfficial { get; set; }
    public bool IsOutdated { get; set; }
    public bool IsOrphan { get; set; }
    public List<string>? Pages { get; set; }
    public string? TextContent { get; set; }
    public int? TeamUnlockPrice { get; set; }

  }

    }

    // Translation permission details response
    public class TranslationPermissionResponse
    {
        public int PermissionId { get; set; }
        public int SeriesId { get; set; }
        public string? SeriesTitle { get; set; }
        public int TeamId { get; set; }
        public string? TeamName { get; set; }
        public int LanguageId { get; set; }
        public string? LanguageName { get; set; }
        public string Origin { get; set; } = string.Empty;
        public int GrantedBy { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public DateTime? GrantedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? Note { get; set; }
        public string? Facebook { get; set; }
        public string? Discord { get; set; }
        public string? Website { get; set; }
        public string? Certificates { get; set; }
    }

    // Team series stats response (already exists as standalone, keeping for backward compat)
    public class TeamSeriesResponse
    {
        public int SeriesId { get; set; }
        public int PermissionId { get; set; }
        public int LanguageId { get; set; }
        public string LanguageName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? CoverImageUrl { get; set; }
        public string Status { get; set; } = string.Empty;
        public int TotalChapters { get; set; }
        public DateTime? LastUpdate { get; set; }
        public int Views { get; set; }
        public decimal Rating { get; set; }
    }

    // Team stats summary response
    public class TeamStatsResponse
    {
        public int TotalViews { get; set; }
        public int TotalBookmarks { get; set; }
        public int ActiveSeriesCount { get; set; }
        public int TotalChaptersTranslated { get; set; }
        public decimal AverageRating { get; set; }
    }
}
