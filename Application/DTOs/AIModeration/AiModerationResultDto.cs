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

		// Các category bị flag, ví dụ: "violence, sexual"
		// Null nếu không bị flag
		public string? FlaggedReason { get; set; }
	}
}
