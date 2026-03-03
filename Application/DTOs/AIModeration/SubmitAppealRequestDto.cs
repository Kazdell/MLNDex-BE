using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.AIModeration
{
	/// <summary>
	/// Request tác giả gửi lên khi muốn appeal
	/// </summary>
	public class SubmitAppealRequestDto
	{
		public string AppealReason { get; set; } = string.Empty;
	}
}
