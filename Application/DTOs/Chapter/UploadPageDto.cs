using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Chapter
{
  /// Mỗi trang truyện gửi lên từ Controller
  public class UploadPageDto
  {
    public Stream FileStream { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public int PageNumber { get; set; }
  }
}
