using Application.DTOs.AIModeration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.AIModeration
{
	public interface IAiModerationClient
	{
		/// <summary>
		/// Kiểm duyệt nhiều ảnh của 1 chapter.
		/// Trả về flagged = true nếu BẤT KỲ trang nào có vấn đề.
		/// </summary>
		/// <param name="imageUrls">Danh sách URL ảnh các trang trong chapter</param>
		Task<AiModerationResultDto> ModerateImagesAsync(IEnumerable<string> imageUrls);
	}
}
