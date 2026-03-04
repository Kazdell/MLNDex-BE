using Application.DTOs.Chapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Creator
{
	public interface IChapterPageService
	{
		/// <summary>
		/// Upload danh sách trang cho một chapter.
		/// Tự động: upload R2/Cloudinary → lưu URL vào DB → gửi AI kiểm duyệt.
		/// </summary>
		Task<UploadChapterPagesResponseDto> UploadPagesAsync(
			int chapterId,
			List<UploadPageDto> pages,
			CancellationToken cancellationToken = default);

		/// <summary>Xóa 1 trang: xóa ảnh trên Cloudinary + xóa record DB</summary>
		Task DeletePageAsync(int pageId, CancellationToken cancellationToken = default);

		/// <summary>Xóa toàn bộ trang của chapter (dùng khi xóa chapter)</summary>
		Task DeleteAllPagesAsync(int chapterId, CancellationToken cancellationToken = default);
	}
}
