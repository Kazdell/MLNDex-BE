using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.AIModeration
{
	public class AiModerationResultDto
	{
		/// <summary>
		/// Kết quả AI trả về sau khi kiểm duyệt ảnh.
		/// Chỉ dùng nội bộ giữa IAiModerationClient và ModerationService.
		/// </summary>

		public bool Flagged { get; set; }
		public string? FlaggedReason { get; set; }

		// Điểm số chi tiết của từng category (lấy mức cao nhất qua các trang)
		public Dictionary<string, double> CategoryScores { get; set; } = new();
	}
}
