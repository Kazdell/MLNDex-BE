using Application.DTOs.AIModeration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Chapter
{
  public class CreateChapterResponseDto
  {
    public int ChapterId { get; set; }
    public int SeriesId { get; set; }
    public float ChapterNumber { get; set; }
    public string? Title { get; set; }
    public int PageCount { get; set; }
  }
}
