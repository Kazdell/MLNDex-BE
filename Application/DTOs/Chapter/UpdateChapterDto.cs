using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Chapter
{
    public class UpdateChapterDto
    {
        public int SeriesId { get; set; }
        public float ChapterNumber { get; set; }
        public string? Title { get; set; }
        public int? LanguageId { get; set; }
        // Defines the sequence of pages. Example: [{"type":"existing", "id": 12}, {"type":"new", "fileIndex": 0}]
        public string? PageLayoutJson { get; set; } 
    }
}
