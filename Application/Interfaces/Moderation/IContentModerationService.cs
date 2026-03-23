using Application.DTOs.Moderation;

namespace Application.Interfaces.Moderation
{
  public interface IContentModerationService
  {
    Task<ModerationQueueDto> DecideAsync(
        int queueId,
        int moderatorId,
        ContentModerationDecisionRequest request,
        CancellationToken cancellationToken = default
    );
  }
}
