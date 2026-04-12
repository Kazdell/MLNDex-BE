using Application.DTOs.Moderation;
using Application.Interfaces.Data;
using Application.Interfaces.Moderation;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Moderation
{
  public class ViolationFeedbackService : IViolationFeedbackService
  {
    private readonly IMlndexDbContext _context;

    public ViolationFeedbackService(IMlndexDbContext context)
    {
      _context = context;
    }

    public async Task<ViolationFeedbackDto> SendFeedbackAsync(
        int queueId,
        int moderatorId,
        ViolationFeedbackRequest request,
        CancellationToken cancellationToken = default
    )
    {
      var queue =
          await _context.ModerationQueues.FirstOrDefaultAsync(
              q => q.QueueId == queueId,
              cancellationToken
          ) ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.MODERATION_QUEUE_NOT_FOUND);

      var targetUserId =
          await ResolveTargetUserId(queue, cancellationToken)
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

      // Add notification to creator
      _context.Notifications.Add(
          new Notification
          {
            UserId = targetUserId,
            NotificationType = NotificationType.CONTENT_REJECTED,
            Title = "Phản hồi vi phạm",
            Message = request.Message,
            ActionUrl = string.Empty,
            RelatedEntityId = queue.ContentId,
            RelatedEntityType = queue.ContentType.ToString(),
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
          }
      );

      _context.ModerationActions.Add(
          new ModerationAction
          {
            QueueId = queue.QueueId,
            ModeratorId = moderatorId,
            Action = ModerationActionType.FlagForReview,
            Reason = request.Message,
            ActedAt = DateTime.UtcNow,
          }
      );

      await _context.SaveChangesAsync(cancellationToken);

      return new ViolationFeedbackDto
      {
        QueueId = queue.QueueId,
        TargetContentId = queue.ContentId,
        Message = request.Message,
        CreatedAt = DateTime.UtcNow,
      };
    }

    private async Task<int?> ResolveTargetUserId(
        ModerationQueue queue,
        CancellationToken cancellationToken
    )
    {
      switch (queue.ContentType)
      {
        case ModerationQueueContentType.SERIES:
          var series = await _context.Series.FirstOrDefaultAsync(
              s => s.SeriesId == queue.ContentId,
              cancellationToken
          );
          return series?.Creator?.UserId;
        case ModerationQueueContentType.CHAPTER:
          var chapter = await _context
              .Chapters.Include(c => c.Series)
                  .ThenInclude(s => s.Creator)
              .FirstOrDefaultAsync(
                  c => c.ChapterId == queue.ContentId,
                  cancellationToken
              );
          return chapter?.Series.Creator.UserId;
        case ModerationQueueContentType.TRANSLATION:
          var translation = await _context
              .Translations.Include(t => t.Chapter)
                  .ThenInclude(c => c.Series)
                      .ThenInclude(s => s.Creator)
              .FirstOrDefaultAsync(
                  t => t.TranslationId == queue.ContentId,
                  cancellationToken
              );
          return translation?.Chapter.Series.Creator.UserId;
        default:
          return null;
      }
    }
  }
}
