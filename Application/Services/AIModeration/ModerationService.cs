using Application.Interfaces.Data;
using Application.Interfaces.AIModeration;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services.AIModeration
{
	public class ModerationService : IModerationService
	{
		private readonly IMlndexDbContext _db;
		private readonly IAiModerationClient _aiClient;
		private readonly ILogger<ModerationService> _logger;

		public ModerationService(
			IMlndexDbContext db,
			IAiModerationClient aiClient,
			ILogger<ModerationService> logger)
		{
			_db = db;
			_aiClient = aiClient;
			_logger = logger;
		}

		// ─────────────────────────────────────────────────────────────
		// Bước 1: AI tự động kiểm duyệt khi chapter vừa upload
		// ─────────────────────────────────────────────────────────────
		public async Task RunAiModerationAsync(int chapterId)
		{
			var chapter = await _db.Chapters
				.Include(c => c.Pages)
				.FirstOrDefaultAsync(c => c.ChapterId == chapterId)
				?? throw new KeyNotFoundException($"Không tìm thấy chapter {chapterId}");

			_logger.LogInformation("Chạy AI kiểm duyệt chapter {ChapterId}", chapterId);

			// Lấy toàn bộ URL ảnh trong chapter
			var imageUrls = chapter.Pages
				.OrderBy(p => p.PageNumber)
				.Select(p => p.ImageUrl)
				.ToList();

			if (imageUrls.Count == 0)
			{
				_logger.LogWarning("Chapter {ChapterId} không có ảnh nào", chapterId);
				return;
			}

			// Gọi AI
			var aiResult = await _aiClient.ModerateImagesAsync(imageUrls);

			// Cập nhật ModerationStatus trên Chapter
			chapter.ModerationStatus = aiResult.Flagged
				? ModerationStatus.REJECTED
				: ModerationStatus.APPROVED;

			await _db.SaveChangesAsync();

			if (aiResult.Flagged)
			{
				_logger.LogWarning("Chapter {ChapterId} bị flag: {Reason}", chapterId, aiResult.FlaggedReason);
				// TODO: Gửi notification cho tác giả (sau này thêm INotificationService vào đây)
			}
			else
			{
				_logger.LogInformation("Chapter {ChapterId} đã được AI tự động duyệt", chapterId);
			}
		}

		// ─────────────────────────────────────────────────────────────
		// Bước 2: Tác giả appeal → tạo queue cho moderator xử lý
		// ─────────────────────────────────────────────────────────────
		public async Task SubmitAppealAsync(int chapterId, int requestedByUserId, string appealReason)
		{
			var chapter = await _db.Chapters
				.FirstOrDefaultAsync(c => c.ChapterId == chapterId)
				?? throw new KeyNotFoundException($"Không tìm thấy chapter {chapterId}");

			// Chỉ cho appeal khi đang bị Flagged
			if (chapter.ModerationStatus != ModerationStatus.REJECTED)
				throw new InvalidOperationException("Chỉ có thể appeal khi chapter đang bị reject.");

			// Đếm số lần đã appeal trước đó
			var appealCount = await _db.ModerationQueues
				.CountAsync(q => q.ContentId == chapterId
							  && q.ContentType == ModerationQueueContentType.CHAPTER
							  && q.Source == QueueSource.AI_FLAGGED);

			// Tạo queue item mới cho moderator
			var queue = new ModerationQueue
			{
				ContentId = chapterId,
				ContentType = ModerationQueueContentType.CHAPTER,
				Source = QueueSource.AI_FLAGGED,
				Priority = QueuePriority.MEDIUM,
				Status = QueueStatus.PENDING,
				ReportCount = 0,
				FlaggedAt = DateTime.UtcNow,
				AppealReason = appealReason,
				AppealCount = appealCount + 1,

				// Ghi lại lý do AI đã flag trước đó để mod có context
				AiFlagged = true,
				AiFlaggedReason = await GetLastAiFlaggedReasonAsync(chapterId),
				AiProcessedAt = DateTime.UtcNow,
			};

			_db.ModerationQueues.Add(queue);
			await _db.SaveChangesAsync();

			_logger.LogInformation(
				"Tác giả {UserId} appeal chapter {ChapterId} lần {Count}",
				requestedByUserId, chapterId, appealCount + 1);
		}

		// ─────────────────────────────────────────────────────────────
		// Private helpers
		// ─────────────────────────────────────────────────────────────

		private async Task<string?> GetLastAiFlaggedReasonAsync(int chapterId)
		{
			return await _db.ModerationQueues
				.Where(q => q.ContentId == chapterId
						 && q.ContentType == ModerationQueueContentType.CHAPTER
						 && q.Source == QueueSource.AI_FLAGGED)
				.OrderByDescending(q => q.FlaggedAt)
				.Select(q => q.AiFlaggedReason)
				.FirstOrDefaultAsync();
		}
	}
}
