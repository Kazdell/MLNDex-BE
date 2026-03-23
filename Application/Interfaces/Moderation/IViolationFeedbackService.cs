using Application.DTOs.Moderation;

namespace Application.Interfaces.Moderation
{
  public interface IViolationFeedbackService
  {
    Task<ViolationFeedbackDto> SendFeedbackAsync(
        int queueId,
        int moderatorId,
        ViolationFeedbackRequest request,
        CancellationToken cancellationToken = default
    );
  }
}
