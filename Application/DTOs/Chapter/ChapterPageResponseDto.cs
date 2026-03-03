using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Chapter
{
	// Thông tin 1 trang sau khi upload thành công
	public class ChapterPageResponseDto
	{
		public int PageId { get; set; }
		public int ChapterId { get; set; }
		public int PageNumber { get; set; }
		public string ImageUrl { get; set; } = default!;
	}
}
