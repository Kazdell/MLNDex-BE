using Application.DTOs.Moderation;
using Application.Interfaces.Data;
using Application.Interfaces.Moderation;
using Application.Interfaces.Notification;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Moderation
{
  public class ReportService : IReportService
  {
    private readonly IMlndexDbContext _context;
    private readonly INotificationService _notificationService;

    public ReportService(IMlndexDbContext context, INotificationService notificationService)
    {
      _context = context;
      _notificationService = notificationService;
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

      // Thông báo cho Moderator & Admin
      var moderators = await _context.Users
          .Where(u => u.UserRoles.Any(ur => ur.Role.RoleName == RoleName.MODERATOR || ur.Role.RoleName == RoleName.ADMIN))
          .Select(u => u.UserId)
          .ToListAsync(cancellationToken);

      foreach (var modId in moderators)
      {
        await _notificationService.CreateNotificationAsync(modId,
            "Có báo cáo mới",
            $"Có báo cáo vi phạm mới (#{report.ReportId}) cần xem xét.",
            "/moderator/dashboard",
            NotificationType.MOD_NEW_REPORT);
      }

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
              ) ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.MODERATION_QUEUE_NOT_FOUND);

      if (queue.Status == QueueStatus.RESOLVED || queue.Status == QueueStatus.DISMISSED)
      {
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);
      }

      if (request.Status == QueueStatus.PENDING)
      {
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);
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

      // Thông báo cho tất cả người đã báo cáo trong queue này
      string statusText = request.Status == QueueStatus.RESOLVED ? "đã được Chấp nhận và Xử lý" : "đã bị Từ chối";
      foreach (var r in queue.Reports)
      {
        await _notificationService.CreateNotificationAsync(r.ReporterId,
            "Cập nhật báo cáo",
            $"Báo cáo #{r.ReportId} của bạn {statusText}.",
            "#",
            NotificationType.REPORT_RESOLVED);
      }

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

    public async Task<ModeratorDashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
      var now = DateTime.UtcNow;
      var stats = new ModeratorDashboardStatsDto();

      // Counts
      stats.TotalReports = await _context.ModerationQueues.CountAsync(cancellationToken);
      stats.NewReports = await _context.ModerationQueues.CountAsync(q => q.Status == QueueStatus.PENDING, cancellationToken);
      stats.ProcessingReports = await _context.ModerationQueues.CountAsync(q => q.Status == QueueStatus.IN_REVIEW, cancellationToken);
      stats.ResolvedReports = await _context.ModerationQueues.CountAsync(q => q.Status == QueueStatus.RESOLVED || q.Status == QueueStatus.DISMISSED, cancellationToken);

      // Week Data (Last 7 days)
      for (int i = 6; i >= 0; i--)
      {
        var date = now.AddDays(-i).Date;
        var dayName = date.DayOfWeek switch
        {
          DayOfWeek.Monday => "T2",
          DayOfWeek.Tuesday => "T3",
          DayOfWeek.Wednesday => "T4",
          DayOfWeek.Thursday => "T5",
          DayOfWeek.Friday => "T6",
          DayOfWeek.Saturday => "T7",
          DayOfWeek.Sunday => "CN",
          _ => date.DayOfWeek.ToString().Substring(0, 3)
        };

        var incoming = await _context.ModerationQueues.CountAsync(q => q.FlaggedAt.Date == date, cancellationToken);
        var processed = await _context.ModerationActions.CountAsync(a => a.ActedAt.Date == date, cancellationToken);

        stats.WeekData.Add(new DailyModerationStatDto
        {
          Day = dayName,
          Incoming = incoming,
          Processed = processed
        });
      }

      // Recent Activities (Last 10 actions)
      var recentActions = await _context.ModerationActions
          .Include(a => a.Moderator)
          .Include(a => a.Queue)
          .OrderByDescending(a => a.ActedAt)
          .Take(10)
          .ToListAsync(cancellationToken);

      foreach (var action in recentActions)
      {
        string contentTitle = action.Queue.ContentType == ModerationQueueContentType.SERIES
            ? (await _context.Series.Where(s => s.SeriesId == action.Queue.ContentId).Select(s => s.Title).FirstOrDefaultAsync(cancellationToken) ?? "Nội dung")
            : (await _context.Chapters.Where(c => c.ChapterId == action.Queue.ContentId).Select(c => c.Title).FirstOrDefaultAsync(cancellationToken) ?? "Chương");

        bool isNegative = action.Action == ModerationActionType.AutoReject || action.Action == ModerationActionType.InstantBan;
        if (action.Action == ModerationActionType.FlagForReview)
        {
            // Human decisions: check final queue status
            isNegative = action.Queue.Status == QueueStatus.DISMISSED;
        }

        string actionText = isNegative ? "đã từ chối" : "đã phê duyệt";
        string color = isNegative ? "bg-red-500" : "bg-green-500";

        stats.Activities.Add(new SystemActivityDto
        {
          ModeratorName = action.Moderator.DisplayName ?? action.Moderator.Username,
          Action = actionText,
          Time = action.ActedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
          Color = color,
          Text = $"{action.Moderator.DisplayName ?? action.Moderator.Username} {actionText} báo cáo cho '{contentTitle}'"
        });
      }

      return stats;
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
