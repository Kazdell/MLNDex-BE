using Application.DTOs.Moderation;
using Application.Interfaces.Data;
using Application.Interfaces.Moderation;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Moderation
{
    public class ReportService : IReportService
    {
        private readonly IMlndexDbContext _context;

        public ReportService(IMlndexDbContext context)
        {
            _context = context;
        }

        public async Task<ReportDto> CreateAsync(
            int reporterId,
            CreateReportRequest request,
            CancellationToken cancellationToken = default
        )
        {
            var queueContentType = MapContentType(request.ContentType);

            var queue = await _context.ModerationQueues.FirstOrDefaultAsync(
                q => q.ContentId == request.ContentId && q.ContentType == queueContentType,
                cancellationToken
            );

            if (queue == null)
            {
                queue = new ModerationQueue
                {
                    ContentId = request.ContentId,
                    ContentType = queueContentType,
                    Priority = QueuePriority.MEDIUM,
                    Status = QueueStatus.PENDING,
                    FlaggedAt = DateTime.UtcNow,
                    ReportCount = 0,
                };
                _context.ModerationQueues.Add(queue);
            }

            queue.ReportCount += 1;

            var report = new Report
            {
                ReporterId = reporterId,
                ContentId = request.ContentId,
                ContentType = request.ContentType,
                Reason = request.Reason,
                Description = request.Description,
                Queue = queue,
                CreatedAt = DateTime.UtcNow,
            };

            _context.Reports.Add(report);
            await _context.SaveChangesAsync(cancellationToken);

            return new ReportDto
            {
                ReportId = report.ReportId,
                ContentId = report.ContentId,
                ContentType = report.ContentType,
                Reason = report.Reason,
                Description = report.Description,
                CreatedAt = report.CreatedAt,
            };
        }

        public async Task<ModerationQueueListResponse> GetPendingQueuesAsync(
            CancellationToken cancellationToken = default
        )
        {
            var items = await _context.ModerationQueues
                .Include(q => q.Reports)
                .Where(q =>
                    q.Status == QueueStatus.PENDING || q.Status == QueueStatus.IN_REVIEW
                )
                .OrderByDescending(q => q.Priority)
                .ThenByDescending(q => q.FlaggedAt)
                .Select(q => new ModerationQueueDto
                {
                    QueueId = q.QueueId,
                    ContentId = q.ContentId,
                    ContentType = q.ContentType,
                    Priority = q.Priority,
                    Status = q.Status,
                    ReportCount = q.ReportCount,
                    FlaggedAt = q.FlaggedAt,
                    AppealReason = q.AppealReason,
                    ContentTitle = q.ContentType == ModerationQueueContentType.SERIES 
                        ? _context.Series.Where(s => s.SeriesId == q.ContentId).Select(s => s.Title).FirstOrDefault()
                        : _context.Chapters.Where(c => c.ChapterId == q.ContentId).Select(c => c.Title).FirstOrDefault(),
                    AuthorName = q.ContentType == ModerationQueueContentType.SERIES
                        ? _context.Series.Where(s => s.SeriesId == q.ContentId).Select(s => s.Creator.PenName).FirstOrDefault()
                        : _context.Chapters.Where(c => c.ChapterId == q.ContentId).Select(c => c.Series.Creator.PenName).FirstOrDefault(),
                    Reports = q.Reports.Select(r => new ReportDto
                    {
                        ReportId = r.ReportId,
                        ContentId = r.ContentId,
                        ContentType = r.ContentType,
                        Reason = r.Reason,
                        Description = r.Description,
                        CreatedAt = r.CreatedAt
                    }).ToList()
                })
                .ToListAsync(cancellationToken);

            return new ModerationQueueListResponse { Items = items };
        }

        public async Task<ModerationQueueDto> DecideAsync(
            int queueId,
            int moderatorId,
            ModerationDecisionRequest request,
            CancellationToken cancellationToken = default
        )
        {
            var queue =
                await _context.ModerationQueues
                    .Include(q => q.Reports)
                    .FirstOrDefaultAsync(
                        q => q.QueueId == queueId,
                        cancellationToken
                    ) ?? throw new KeyNotFoundException("Queue item không tồn tại.");

            if (queue.Status == QueueStatus.RESOLVED || queue.Status == QueueStatus.DISMISSED)
            {
                throw new InvalidOperationException("Queue đã được xử lý.");
            }

            if (request.Status == QueueStatus.PENDING)
            {
                throw new InvalidOperationException("Không thể chuyển về trạng thái PENDING.");
            }

            queue.Status = request.Status;
            queue.AssignedTo = moderatorId;
            queue.AssignedAt = DateTime.UtcNow;

            var action = new ModerationAction
            {
                QueueId = queue.QueueId,
                ModeratorId = moderatorId,
                Action = ModerationActionType.FlagForReview,
                Reason = request.Reason,
                ActedAt = DateTime.UtcNow,
            };

            _context.ModerationActions.Add(action);
            await _context.SaveChangesAsync(cancellationToken);

            return new ModerationQueueDto
            {
                QueueId = queue.QueueId,
                ContentId = queue.ContentId,
                ContentType = queue.ContentType,
                Priority = queue.Priority,
                Status = queue.Status,
                ReportCount = queue.ReportCount,
                FlaggedAt = queue.FlaggedAt,
                AppealReason = queue.AppealReason,
                Reports = queue.Reports.Select(r => new ReportDto
                {
                    ReportId = r.ReportId,
                    ContentId = r.ContentId,
                    ContentType = r.ContentType,
                    Reason = r.Reason,
                    Description = r.Description,
                    CreatedAt = r.CreatedAt
                }).ToList()
            };
        }

        private static ModerationQueueContentType MapContentType(ReportTargetType type)
        {
            return type switch
            {
                ReportTargetType.Series => ModerationQueueContentType.SERIES,
                ReportTargetType.ChapterTranslation => ModerationQueueContentType.CHAPTER,
                ReportTargetType.Team => ModerationQueueContentType.SERIES, // fallback
                ReportTargetType.User => ModerationQueueContentType.SERIES, // fallback
                _ => ModerationQueueContentType.SERIES,
            };
        }
    }
}
