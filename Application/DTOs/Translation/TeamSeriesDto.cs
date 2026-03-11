using System;

namespace Application.DTOs.Translation
{
    public class TeamSeriesDto
    {
        public int SeriesId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? CoverImageUrl { get; set; }
        public string Status { get; set; } = string.Empty;
        public int TotalChapters { get; set; }
        public DateTime? LastUpdate { get; set; }
        public int Views { get; set; }
        public decimal Rating { get; set; }
    }
}
