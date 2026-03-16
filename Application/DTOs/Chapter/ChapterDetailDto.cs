using System;
using System.Collections.Generic;

namespace Application.DTOs.Chapter
{
  public class ChapterDetailDto
  {
    public int ChapterId { get; set; }
    public int SeriesId { get; set; }
    public string? SeriesTitle { get; set; }
    public string? UploaderName { get; set; }
    public string? TranslatorTeamName { get; set; }
    public float ChapterNumber { get; set; }
    public string? Title { get; set; }
    public int? PrevChapterId { get; set; }
    public int? NextChapterId { get; set; }
    public List<ChapterPageResponseDto> Pages { get; set; } = new();
    public List<ChapterSummaryDto> Chapters { get; set; } = new();
    public string ModerationStatus { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? ModerationReason { get; set; }
  }
}
