using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Chapter
{
    public class CreateChapterDto
    {
        public int SeriesId { get; set; }
        public float ChapterNumber { get; set; }
        public string? Title { get; set; }
        public string? Language { get; set; }
        public List<UploadPageDto>? Pages { get; set; }
    }
}
