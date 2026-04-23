using Application.DTOs.Moderation;
using Application.DTOs.ReportSystem;
using Application.Interfaces.Data;
using Application.Interfaces.Moderation;
using Application.Interfaces.Notification;
using Application.Interfaces.ReportSystem;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Moderation
{
  public class ContentModerationService : IContentModerationService
  {
    private readonly IMlndexDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IReputationService _reputationService;

    public ContentModerationService(
        IMlndexDbContext context,
        INotificationService notificationService,
        IReputationService reputationService)
    {
      _context = context;
      _notificationService = notificationService;
      _reputationService = reputationService;
    }

    public async Task<ModerationQueueDto> DecideAsync(
        int queueId,
        int moderatorId,
        ContentModerationDecisionRequest request,
        CancellationToken cancellationToken = default
    )
    {
      var queue =
          await _context.ModerationQueues.FirstOrDefaultAsync(
              q => q.QueueId == queueId,
              cancellationToken
          ) ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.MODERATION_QUEUE_NOT_FOUND, "Moderation queue not found", 404);

      if (queue.Status == QueueStatus.RESOLVED || queue.Status == QueueStatus.DISMISSED || queue.Status == QueueStatus.REJECTED)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED, "Cannot decide on an already resolved/rejected queue", 409);

      var targetStatus = request.Action switch
      {
        ContentDecisionAction.APPROVE => ModerationStatus.APPROVED,
        ContentDecisionAction.REJECT => ModerationStatus.REJECTED,
        ContentDecisionAction.BAN => ModerationStatus.BANNED,
        _ => ModerationStatus.PENDING,
      };

      await UpdateContentStatusAsync(queue, targetStatus, cancellationToken);

      queue.Status = request.Action == ContentDecisionAction.APPROVE 
          ? QueueStatus.RESOLVED 
          : QueueStatus.REJECTED;
      queue.AssignedTo = moderatorId;
      queue.AssignedAt = DateTime.UtcNow;

      var actionType = request.Action switch
      {
        ContentDecisionAction.APPROVE => ModerationActionType.AutoPass,
        ContentDecisionAction.REJECT => ModerationActionType.AutoReject,
        ContentDecisionAction.BAN => ModerationActionType.InstantBan,
        _ => ModerationActionType.FlagForReview,
      };

      _context.ModerationActions.Add(
          new ModerationAction
          {
            QueueId = queue.QueueId,
            ModeratorId = moderatorId,
            Action = actionType,
            Reason = request.Reason ?? string.Empty,
            ActedAt = DateTime.UtcNow,
          }
      );

      await _context.SaveChangesAsync(cancellationToken);

      // ── Trừ 10 điểm uy tín khi REJECT / BAN ─────────────────────────
      if (request.Action == ContentDecisionAction.REJECT || request.Action == ContentDecisionAction.BAN)
      {
        await DeductReputationForQueueAsync(queue, cancellationToken);
      }
      // ─────────────────────────────────────────────────────────────────

      // Send notification to content owner
      await SendModerationNotificationAsync(queue, targetStatus, request.Reason, cancellationToken);

      return new ModerationQueueDto
      {
        QueueId = queue.QueueId,
        ContentId = queue.ContentId,
        ContentType = queue.ContentType,
        Priority = queue.Priority,
        Status = queue.Status,
        ReportCount = queue.ReportCount,
        FlaggedAt = queue.FlaggedAt,
        AppealReason = queue.AppealReason
      };
    }

    private async Task UpdateContentStatusAsync(
        ModerationQueue queue,
        ModerationStatus status,
        CancellationToken cancellationToken
    )
    {
      switch (queue.ContentType)
      {
        case ModerationQueueContentType.SERIES:
          var series =
              await _context.Series.FirstOrDefaultAsync(
                  s => s.SeriesId == queue.ContentId,
                  cancellationToken
              ) ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.SERIES_NOT_FOUND);
          series.ModerationStatus = status;
          break;
        case ModerationQueueContentType.CHAPTER:
          var chapter =
              await _context.Chapters.FirstOrDefaultAsync(
                  c => c.ChapterId == queue.ContentId,
                  cancellationToken
              ) ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.CHAPTER_NOT_FOUND);
          chapter.ModerationStatus = status;
          // Auto-publish when approved, keep DRAFT otherwise
          if (status == ModerationStatus.APPROVED)
          {
            chapter.Status = ChapterStatus.PUBLISHED;
            chapter.PublishedAt = DateTime.UtcNow;
          }
          else
          {
            chapter.Status = ChapterStatus.DRAFT;
          }
          break;
        case ModerationQueueContentType.TRANSLATION:
          var translation =
              await _context.Translations.FirstOrDefaultAsync(
                  t => t.TranslationId == queue.ContentId,
                  cancellationToken
              ) ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.TRANSLATION_NOT_FOUND);
          translation.ModerationStatus = status;
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }
    }

    private async Task SendModerationNotificationAsync(ModerationQueue queue, ModerationStatus status, string? reason, CancellationToken cancellationToken)
    {
      int? ownerUserId = null;
      string contentTitle = "";
      string actionUrl = "";

      switch (queue.ContentType)
      {
        case ModerationQueueContentType.SERIES:
          var series = await _context.Series.FirstOrDefaultAsync(s => s.SeriesId == queue.ContentId, cancellationToken);
          if (series != null)
          {
            ownerUserId = series.CreatorId;
            contentTitle = series.Title;
            actionUrl = $"/series/{series.SeriesId}";
          }
          break;
        case ModerationQueueContentType.CHAPTER:
          var chapter = await _context.Chapters.Include(c => c.Series).FirstOrDefaultAsync(c => c.ChapterId == queue.ContentId, cancellationToken);
          if (chapter != null)
          {
            ownerUserId = chapter.Series.CreatorId;
            contentTitle = $"Chapter {chapter.ChapterNumber} của {chapter.Series.Title}";
            actionUrl = $"/series/{chapter.SeriesId}/chapters/{chapter.ChapterId}";
          }
          break;
        case ModerationQueueContentType.TRANSLATION:
          var translation = await _context.Translations.Include(t => t.Chapter).ThenInclude(c => c.Series).Include(t => t.Permission).ThenInclude(p => p!.Team).FirstOrDefaultAsync(t => t.TranslationId == queue.ContentId, cancellationToken);
          if (translation != null)
          {
            ownerUserId = translation.Permission!.Team!.LeaderId;
            contentTitle = $"Bản dịch của {translation.Chapter.Series.Title}";
            actionUrl = $"/series/{translation.Chapter.SeriesId}/chapters/{translation.ChapterId}";
          }
          break;
      }

      if (ownerUserId.HasValue)
      {
        var type = status switch
        {
          ModerationStatus.APPROVED => NotificationType.CONTENT_APPROVED,
          ModerationStatus.REJECTED => NotificationType.CONTENT_REJECTED,
          ModerationStatus.BANNED => NotificationType.CONTENT_BANNED,
          _ => NotificationType.SYSTEM
        };

        var statusText = status switch
        {
          ModerationStatus.APPROVED => "được duyệt",
          ModerationStatus.REJECTED => "bị từ chối",
          ModerationStatus.BANNED => "bị khóa",
          _ => "xử lý"
        };

        var message = $"Nội dung '{contentTitle}' đã {statusText}.";
        if (!string.IsNullOrEmpty(reason)) message += $" Lý do: {reason}";

        await _notificationService.CreateNotificationAsync(
            ownerUserId.Value,
            "Kiểm duyệt nội dung",
            message,
            actionUrl,
            type
        );
      }
    }

    // ── Trừ 10 điểm uy tín cho Creator/Team sở hữu nội dung bị xử lý ──
    private async Task DeductReputationForQueueAsync(ModerationQueue queue, CancellationToken cancellationToken)
    {
      try
      {
        switch (queue.ContentType)
        {
          case ModerationQueueContentType.SERIES:
            var series = await _context.Series
                .Include(s => s.Creator)
                .FirstOrDefaultAsync(s => s.SeriesId == queue.ContentId, cancellationToken);
            if (series?.Creator != null)
              await _reputationService.ModifyReputationAsync(
                  ReputationTargetType.Creator, series.Creator.CreatorId,
                  -10, $"[Moderator] Nội dung vi phạm bị xử lý (Series #{queue.ContentId})",
                  ct: cancellationToken);
            break;

          case ModerationQueueContentType.CHAPTER:
            var chapter = await _context.Chapters
                .Include(c => c.Series).ThenInclude(s => s.Creator)
                .FirstOrDefaultAsync(c => c.ChapterId == queue.ContentId, cancellationToken);
            if (chapter?.Series?.Creator != null)
              await _reputationService.ModifyReputationAsync(
                  ReputationTargetType.Creator, chapter.Series.Creator.CreatorId,
                  -10, $"[Moderator] Nội dung vi phạm bị xử lý (Chapter #{queue.ContentId})",
                  ct: cancellationToken);
            break;

          case ModerationQueueContentType.TRANSLATION:
            var translation = await _context.Translations
                .Include(t => t.Permission).ThenInclude(p => p!.Team)
                .FirstOrDefaultAsync(t => t.TranslationId == queue.ContentId, cancellationToken);
            if (translation?.Permission?.Team != null)
              await _reputationService.ModifyReputationAsync(
                  ReputationTargetType.Team, translation.Permission.Team.TeamId,
                  -10, $"[Moderator] Bản dịch vi phạm bị xử lý (Translation #{queue.ContentId})",
                  ct: cancellationToken);
            break;
        }
      }
      catch (Exception)
      {
        // Log nhưng không fail toàn bộ request nếu reputation update lỗi
      }
    }
  }
}
