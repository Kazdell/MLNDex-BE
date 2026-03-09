using Application.DTOs.Moderation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.AIModeration
{
	public interface IModerationService
	{
		
		/// Chạy AI kiểm duyệt khi chapter/translation vừa được upload.
		/// - Safe   → tự động set ModerationStatus = AutoApproved
		/// - Flagged → set ModerationStatus = Flagged, thông báo cho tác giả
		Task RunAiModerationAsync(int chapterId);
		Task RunSeriesModerationAsync(int seriesId);


		/// Tác giả yêu cầu moderator review lại sau khi bị AI flag.
		/// Tạo 1 record mới trong ModerationQueue.
		Task SubmitAppealAsync(int chapterId, int requestedByUserId, string appealReason);

		TextCheckResponse PreCheckText(TextCheckRequest request);
		OpenAiScoreResponse AnalyzeOpenAiScores(OpenAiScoreRequest request);
		List<RejectionTemplateDto> GetRejectionTemplates();
		List<BannedTagDto> GetBannedTags();
	}
}
