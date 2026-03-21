using System;

namespace Application.DTOs.Translation
{
    public class TeamSeriesDto
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
}
