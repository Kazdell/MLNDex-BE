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
        public string Category1 { get; set; } = null!;
        public string? Category2 { get; set; }
        public IFormFile? CoverImage { get; set; }
        // Moderation fields
        public int Violence { get; set; }
        public int Nudity { get; set; }
        public int SexualContent { get; set; }
        public int Language_Score { get; set; }
        public int Substances { get; set; }
        public int SensitiveContent { get; set; }
    }
}
