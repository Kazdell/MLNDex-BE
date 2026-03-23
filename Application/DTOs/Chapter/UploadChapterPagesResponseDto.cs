using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Chapter
{
  /// Kết quả trả về sau khi upload toàn bộ trang của chapter
  public class UploadChapterPagesResponseDto
  {
    public int ChapterId { get; set; }
    public int TotalPages { get; set; }
    public string ModerationStatus { get; set; } = default!;
    public List<ChapterPageResponseDto> Pages { get; set; } = [];
  }
}
