using Application.DTOs.Moderation;

namespace Application.Interfaces.Moderation
{
    public interface IReportService
    {
        Task<ReportDto> CreateAsync(
            int reporterId,
            CreateReportRequest request,
            CancellationToken cancellationToken = default
        );
        Task<ModerationQueueListResponse> GetPendingQueuesAsync(
            bool includeAll = false,
            CancellationToken cancellationToken = default
        );
        Task<ModerationQueueDto> DecideAsync(
            int queueId,
            int moderatorId,
            ModerationDecisionRequest request,
            CancellationToken cancellationToken = default
        );
    }
}
