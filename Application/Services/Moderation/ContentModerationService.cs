using Application.DTOs.Moderation;
using Application.Interfaces.Data;
using Application.Interfaces.Moderation;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Moderation
{
    public class ContentModerationService : IContentModerationService
    {
        private readonly IMlndexDbContext _context;

        public ContentModerationService(IMlndexDbContext context)
        {
            _context = context;
        }

        public async Task<ModerationQueueDto> DecideAsync(int queueId, int moderatorId, ContentModerationDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var queue = await _context.ModerationQueues
                .FirstOrDefaultAsync(q => q.QueueId == queueId, cancellationToken)
                ?? throw new KeyNotFoundException("Queue item không tồn tại.");

            if (queue.Status == QueueStatus.RESOLVED || queue.Status == QueueStatus.DISMISSED)
                throw new InvalidOperationException("Queue đã được xử lý.");

            var targetStatus = request.Action switch
            {
                ContentDecisionAction.APPROVE => ModerationStatus.APPROVED,
                ContentDecisionAction.REJECT => ModerationStatus.REJECTED,
                ContentDecisionAction.BAN => ModerationStatus.BANNED,
                _ => ModerationStatus.PENDING
            };

            await UpdateContentStatusAsync(queue, targetStatus, cancellationToken);

            queue.Status = QueueStatus.RESOLVED;
            queue.AssignedTo = moderatorId;
            queue.AssignedAt = DateTime.UtcNow;

            var actionType = request.Action switch
            {
                ContentDecisionAction.APPROVE => ModerationActionType.AutoPass,
                ContentDecisionAction.REJECT => ModerationActionType.AutoReject,
                ContentDecisionAction.BAN => ModerationActionType.InstantBan,
                _ => ModerationActionType.FlagForReview
            };

            _context.ModerationActions.Add(new ModerationAction
            {
                QueueId = queue.QueueId,
                ModeratorId = moderatorId,
                Action = actionType,
                Reason = request.Reason ?? string.Empty,
                ActedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);

            return new ModerationQueueDto
            {
                QueueId = queue.QueueId,
                ContentId = queue.ContentId,
                ContentType = queue.ContentType,
                Priority = queue.Priority,
                Status = queue.Status,
                ReportCount = queue.ReportCount,
                FlaggedAt = queue.FlaggedAt
            };
        }

        private async Task UpdateContentStatusAsync(ModerationQueue queue, ModerationStatus status, CancellationToken cancellationToken)
        {
            switch (queue.ContentType)
            {
                case ModerationQueueContentType.SERIES:
                    var series = await _context.Series.FirstOrDefaultAsync(s => s.SeriesId == queue.ContentId, cancellationToken)
                        ?? throw new KeyNotFoundException("Series không tồn tại.");
                    series.ModerationStatus = status;
                    break;
                case ModerationQueueContentType.CHAPTER:
                    var chapter = await _context.Chapters.FirstOrDefaultAsync(c => c.ChapterId == queue.ContentId, cancellationToken)
                        ?? throw new KeyNotFoundException("Chapter không tồn tại.");
                    chapter.ModerationStatus = status;
                    break;
                case ModerationQueueContentType.TRANSLATION:
                    var translation = await _context.Translations.FirstOrDefaultAsync(t => t.TranslationId == queue.ContentId, cancellationToken)
                        ?? throw new KeyNotFoundException("Translation không tồn tại.");
                    translation.ModerationStatus = status;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
