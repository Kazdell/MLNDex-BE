using Application.DTOs.Common;
using Application.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Moderation;
using Application.DTOs.ReportSystem;
using Application.Interfaces.Data;
using Application.Interfaces.Moderation;
using Application.Interfaces.Notification;
using Application.Interfaces.ReportSystem;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.ReportSystem
{
  public class PlagiarismReportService : IPlagiarismReportService
  {
    private readonly IMlndexDbContext _context;
    private readonly IAccountModerationService _accountModerationService;
    private readonly INotificationService _notificationService;

    public PlagiarismReportService(
        IMlndexDbContext context,
        IAccountModerationService accountModerationService,
        INotificationService notificationService)
    {
      _context = context;
      _accountModerationService = accountModerationService;
      _notificationService = notificationService;
    }

    public async Task<PlagiarismReportDto> CreateReportAsync(int reporterId, CreatePlagiarismReportRequest request, CancellationToken cancellationToken = default)
    {
      var user = await _context.Users.FindAsync(new object[] { reporterId }, cancellationToken);
      if (user == null)
        throw new AppException(ErrorCodes.USER_NOT_FOUND);

      var report = new Report
      {
        ReporterId = reporterId,
        ContentType = request.TargetType,
        ContentId = request.TargetId,
        Reason = request.Reason,
        Description = request.Description,
        EvidenceUrlsJson = JsonSerializer.Serialize(request.EvidenceUrls),
        Status = ReportStatus.Pending,
        CreatedAt = DateTime.UtcNow
        // QueueId is deliberately left null to isolate from the ModerationQueue system
      };

      _context.Reports.Add(report);
      await _context.SaveChangesAsync(cancellationToken);

      return MapToDto(report, user.Username);
    }

    public async Task<PagedResult<PlagiarismReportDto>> GetPendingReportsAsync(int page = 1, int limit = 20, CancellationToken cancellationToken = default)
    {
      var query = _context.Reports
          .Include(r => r.Reporter)
          .Where(r => r.Status == ReportStatus.Pending || r.Status == ReportStatus.Investigating)
          .OrderByDescending(r => r.CreatedAt)
          .ThenByDescending(r => r.ReportId);

      var totalCount = await query.CountAsync(cancellationToken);

      var reports = await query
          .Skip((page - 1) * limit)
          .Take(limit)
          .ToListAsync(cancellationToken);

      // Batch resolve target names & URLs — eliminates N+1 queries (82 → ~6)
      var targets = await BatchResolveTargetsAsync(reports, cancellationToken);

      var items = reports.Select(r =>
      {
        var dto = MapToDto(r, r.Reporter.Username);
        var target = targets.GetValueOrDefault(r.ReportId, ("Unknown", "#"));
        dto.TargetName = target.Item1;
        dto.TargetUrl = target.Item2;
        return dto;
      }).ToList();

      return new PagedResult<PlagiarismReportDto>(items, totalCount, page, limit);
    }

    public async Task<PlagiarismReportStatsDto> GetReportStatsAsync(CancellationToken cancellationToken = default)
    {
      var stats = await _context.Reports
          .GroupBy(r => 1)
          .Select(g => new PlagiarismReportStatsDto
          {
            Total = g.Count(),
            Pending = g.Count(r => r.Status == ReportStatus.Pending || r.Status == ReportStatus.Investigating),
            Severe = g.Count(r => r.Reason == ReportReason.Plagiarism || r.Reason == ReportReason.Inappropriate),
            Resolved = g.Count(r => r.Status == ReportStatus.Resolved || r.Status == ReportStatus.Rejected)
          })
          .FirstOrDefaultAsync(cancellationToken);

      return stats ?? new PlagiarismReportStatsDto();
    }

    public async Task<PlagiarismReportDto> ResolveReportAsync(int reportId, int moderatorId, ResolvePlagiarismReportRequest request, CancellationToken cancellationToken = default)
    {
      var report = await _context.Reports
          .Include(r => r.Reporter)
          .FirstOrDefaultAsync(r => r.ReportId == reportId, cancellationToken);

      if (report == null)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.REPORT_NOT_FOUND);

      if (report.Status == ReportStatus.Resolved || report.Status == ReportStatus.Rejected)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

      report.Status = request.NewStatus;

      // Chỉ thực hiện xử phạt nếu trạng thái là Resolved
      if (request.NewStatus == ReportStatus.Resolved)
      {
        // 1. Logic Ẩn Content (Strike)
        if (request.StrikeContent)
        {
          await StrikeContentAsync(report, cancellationToken);
        }

        // 2. Logic trừ điểm Trust Score
        if (request.PenaltyScore.HasValue && request.PenaltyScore.Value > 0)
        {
          await ApplyPenaltyAsync(report, request.PenaltyScore.Value, request.ResolutionNotes, cancellationToken);
        }

        // 3. Logic Ban Creator / Send Warning (Cần OwnerId)
        var ownerId = await GetTargetOwnerIdAsync(report.ContentType, report.ContentId, cancellationToken);
        if (ownerId > 0)
        {
          if (request.BanCreator)
          {
            await _accountModerationService.ApplyAsync(ownerId, moderatorId, new AccountActionRequest
            {
              Action = AccountActionType.DEACTIVATE,
              Reason = $"Bị khóa do vi phạm báo cáo #{report.ReportId}: {request.ResolutionNotes}"
            }, cancellationToken);
          }
          else if (request.SendWarning)
          {
            await _notificationService.CreateNotificationAsync(ownerId, "Hệ thống Cảnh cáo",
              $"Nội dung của bạn bị báo cáo vi phạm (#{report.ReportId}). Lý do: {request.ResolutionNotes}. Vui lòng tuân thủ nội quy.",
              "#", NotificationType.SYSTEM);
          }
        }
      }

      await _context.SaveChangesAsync(cancellationToken);

      var dto = MapToDto(report, report.Reporter.Username);
      dto.TargetName = await GetTargetNameAsync(report.ContentType, report.ContentId, cancellationToken);
      dto.TargetUrl = await GetTargetUrlAsync(report.ContentType, report.ContentId, cancellationToken);
      return dto;
    }

    public async Task<CompareTranslationResponse> GetCompareDataAsync(int reportId, int referenceTranslationId, CancellationToken cancellationToken = default)
    {
      var report = await _context.Reports.FindAsync(new object[] { reportId }, cancellationToken);
      if (report == null) throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.REPORT_NOT_FOUND);

      if (report.ContentType != ReportTargetType.ChapterTranslation)
      {
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);
      }

      var reportedTranslation = await _context.Translations
          .Include(t => t.Chapter)
          .Include(t => t.Permission)
              .ThenInclude(p => p!.Team)
          .Include(t => t.TranslationPages)
          .FirstOrDefaultAsync(t => t.TranslationId == report.ContentId, cancellationToken);

      var referenceTranslation = await _context.Translations
          .Include(t => t.Chapter)
          .Include(t => t.Permission)
              .ThenInclude(p => p!.Team)
          .Include(t => t.TranslationPages)
          .FirstOrDefaultAsync(t => t.TranslationId == referenceTranslationId, cancellationToken);

      if (reportedTranslation == null || referenceTranslation == null)
      {
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.TRANSLATION_NOT_FOUND);
      }

      return new CompareTranslationResponse
      {
        Reported = new CompareTranslationDetail
        {
          TranslationId = reportedTranslation.TranslationId,
          ChapterId = reportedTranslation.ChapterId,
          ChapterTitle = reportedTranslation.Chapter?.Title ?? "Unknown",
          TeamName = reportedTranslation.Permission?.Team?.TeamName ?? "Unknown",
          TeamId = reportedTranslation.Permission?.TeamId ?? 0,
          ImageUrls = reportedTranslation.TranslationPages?.OrderBy(p => p.PageNumber).Select(p => p.TranslationImageUrl).ToList() ?? new List<string>()
        },
        Reference = new CompareTranslationDetail
        {
          TranslationId = referenceTranslation.TranslationId,
          ChapterId = referenceTranslation.ChapterId,
          ChapterTitle = referenceTranslation.Chapter?.Title ?? "Unknown",
          TeamName = referenceTranslation.Permission?.Team?.TeamName ?? "Unknown",
          TeamId = referenceTranslation.Permission?.TeamId ?? 0,
          ImageUrls = referenceTranslation.TranslationPages?.OrderBy(p => p.PageNumber).Select(p => p.TranslationImageUrl).ToList() ?? new List<string>()
        }
      };
    }

    private async Task ApplyPenaltyAsync(Report report, int score, string? reason, CancellationToken ct)
    {
      if (report.ContentType == ReportTargetType.ChapterTranslation)
      {
        var trans = await _context.Translations
            .Include(t => t.Permission)
            .FirstOrDefaultAsync(t => t.TranslationId == report.ContentId, ct);

        if (trans != null && trans.Permission != null)
        {
          var team = await _context.TranslationTeams.FindAsync(new object[] { trans.Permission.TeamId }, ct);
          if (team != null)
          {
            team.ReputationScore -= score;
            if (team.ReputationScore <= 0) team.LockStatus = TeamLockStatus.LOCKED;

            _context.ReputationHistories.Add(new ReputationHistory
            {
              TranslationTeamId = team.TeamId,
              ScoreChange = -score,
              Reason = reason ?? "Bị phạt do vi phạm bản quyền/đạo nhái.",
              RelatedReportId = report.ReportId,
              CreatedAt = DateTime.UtcNow
            });
          }
        }
      }
      else if (report.ContentType == ReportTargetType.Team)
      {
        var team = await _context.TranslationTeams.FindAsync(new object[] { report.ContentId }, ct);
        if (team != null)
        {
          team.ReputationScore -= score;
          if (team.ReputationScore <= 0) team.LockStatus = TeamLockStatus.LOCKED;

          _context.ReputationHistories.Add(new ReputationHistory
          {
            TranslationTeamId = team.TeamId,
            ScoreChange = -score,
            Reason = reason ?? "Bị phạt do vi phạm nội quy.",
            RelatedReportId = report.ReportId,
            CreatedAt = DateTime.UtcNow
          });
        }
      }
      else if (report.ContentType == ReportTargetType.User)
      {
        var creator = await _context.CreatorProfiles.FirstOrDefaultAsync(c => c.UserId == report.ContentId, ct);
        if (creator != null)
        {
          creator.ReputationScore -= score;
          
          if (creator.ReputationScore <= 0) 
          {
             var user = await _context.Users.FindAsync(new object[] { report.ContentId }, ct);
             if (user != null) user.CannotUpload = true;
          }

          _context.ReputationHistories.Add(new ReputationHistory
          {
            CreatorId = creator.CreatorId,
            ScoreChange = -score,
            Reason = reason ?? "Bị phạt do vi phạm nội quy/đạo văn.",
            RelatedReportId = report.ReportId,
            CreatedAt = DateTime.UtcNow
          });
        }
      }
    }

    private async Task StrikeContentAsync(Report report, CancellationToken ct)
    {
      if (report.ContentType == ReportTargetType.ChapterTranslation)
      {
        var trans = await _context.Translations.FindAsync(new object[] { report.ContentId }, ct);
        if (trans != null)
        {
          trans.ModerationStatus = ModerationStatus.REJECTED; // Hide or reject
        }
      }
    }

    private PlagiarismReportDto MapToDto(Report report, string reporterName)
    {
      var evidenceUrls = string.IsNullOrEmpty(report.EvidenceUrlsJson) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(report.EvidenceUrlsJson) ?? new List<string>();
      return new PlagiarismReportDto
      {
        ReportId = report.ReportId,
        ReporterId = report.ReporterId,
        ReporterName = reporterName,
        TargetType = report.ContentType,
        TargetId = report.ContentId,
        Reason = report.Reason,
        Description = report.Description ?? string.Empty,
        Status = report.Status,
        CreatedAt = report.CreatedAt,
        EvidenceUrls = evidenceUrls
      };
    }

    private async Task<Dictionary<int, (string Name, string Url)>> BatchResolveTargetsAsync(List<Report> reports, CancellationToken ct)
    {
      var result = new Dictionary<int, (string, string)>();
      if (reports.Count == 0) return result;

      var byType = reports.GroupBy(r => r.ContentType)
          .ToDictionary(g => g.Key, g => g.Select(r => r.ContentId).Distinct().ToList());

      // Batch fetch Series
      Dictionary<int, string> seriesNames = new();
      if (byType.TryGetValue(ReportTargetType.Series, out var seriesIds) && seriesIds.Count > 0)
        seriesNames = await _context.Series.Where(s => seriesIds.Contains(s.SeriesId))
            .ToDictionaryAsync(s => s.SeriesId, s => s.Title, ct);

      // Batch fetch Translations (name + chapterId for URL)
      Dictionary<int, (string Name, int ChapterId)> transData = new();
      if (byType.TryGetValue(ReportTargetType.ChapterTranslation, out var transIds) && transIds.Count > 0)
        transData = await _context.Translations
            .Where(t => transIds.Contains(t.TranslationId))
            .Select(t => new { t.TranslationId, Name = t.Chapter!.Title + " - " + t.Permission!.Team!.TeamName, t.ChapterId })
            .ToDictionaryAsync(x => x.TranslationId, x => (x.Name, x.ChapterId), ct);

      // Batch fetch Teams
      Dictionary<int, string> teamNames = new();
      if (byType.TryGetValue(ReportTargetType.Team, out var teamIds) && teamIds.Count > 0)
        teamNames = await _context.TranslationTeams.Where(t => teamIds.Contains(t.TeamId))
            .ToDictionaryAsync(t => t.TeamId, t => t.TeamName, ct);

      // Batch fetch Users
      Dictionary<int, string> userNames = new();
      if (byType.TryGetValue(ReportTargetType.User, out var userIds) && userIds.Count > 0)
        userNames = await _context.Users.Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.Username, ct);

      // Map back to each report
      foreach (var r in reports)
      {
        var (name, url) = r.ContentType switch
        {
          ReportTargetType.Series => (
              seriesNames.GetValueOrDefault(r.ContentId, "Unknown Series"),
              $"/series/{r.ContentId}"),
          ReportTargetType.ChapterTranslation => transData.TryGetValue(r.ContentId, out var td)
              ? (td.Name, $"/chapter/{td.ChapterId}?translationId={r.ContentId}")
              : ("Unknown Translation", "#"),
          ReportTargetType.Team => (
              teamNames.GetValueOrDefault(r.ContentId, "Unknown Team"),
              $"/translation/dashboard/{r.ContentId}"),
          ReportTargetType.User => (
              userNames.GetValueOrDefault(r.ContentId, "Unknown User"),
              userNames.TryGetValue(r.ContentId, out var uname) ? $"/profile/{uname}" : "#"),
          _ => ("Unknown Target", "#")
        };
        result[r.ReportId] = (name, url);
      }
      return result;
    }

    private async Task<string> GetTargetNameAsync(ReportTargetType targetType, int targetId, CancellationToken ct)
    {
      return targetType switch
      {
        ReportTargetType.Series => await _context.Series.Where(s => s.SeriesId == targetId).Select(s => s.Title).FirstOrDefaultAsync(ct) ?? "Unknown Series",
        ReportTargetType.ChapterTranslation => await _context.Translations
            .Where(t => t.TranslationId == targetId)
            .Select(t => t.Chapter!.Title + " - " + t.Permission!.Team!.TeamName)
            .FirstOrDefaultAsync(ct) ?? "Unknown Translation",
        ReportTargetType.Team => await _context.TranslationTeams.Where(t => t.TeamId == targetId).Select(t => t.TeamName).FirstOrDefaultAsync(ct) ?? "Unknown Team",
        ReportTargetType.User => await _context.Users.Where(u => u.UserId == targetId).Select(u => u.Username).FirstOrDefaultAsync(ct) ?? "Unknown User",
        _ => "Unknown Target"
      };
    }

    private async Task<string> GetTargetUrlAsync(ReportTargetType targetType, int targetId, CancellationToken ct)
    {
      return targetType switch
      {
        ReportTargetType.Series => $"/series/{targetId}",
        ReportTargetType.ChapterTranslation => await _context.Translations
            .Where(t => t.TranslationId == targetId)
            .Select(t => $"/chapter/{t.ChapterId}?translationId={t.TranslationId}")
            .FirstOrDefaultAsync(ct) ?? "#",
        ReportTargetType.Team => $"/translation/dashboard/{targetId}",
        ReportTargetType.User => await _context.Users
            .Where(u => u.UserId == targetId)
            .Select(u => $"/profile/{u.Username}")
            .FirstOrDefaultAsync(ct) ?? "#",
        _ => "#"
      };
    }

    private async Task<int> GetTargetOwnerIdAsync(ReportTargetType targetType, int targetId, CancellationToken ct)
    {
      return targetType switch
      {
        ReportTargetType.Series => await _context.Series.Where(s => s.SeriesId == targetId).Select(s => s.CreatorId).FirstOrDefaultAsync(ct),
        ReportTargetType.ChapterTranslation => await _context.Translations
            .Where(t => t.TranslationId == targetId)
            .Select(t => t.Permission != null ? t.Permission.TeamId : 0)
            .FirstOrDefaultAsync(ct) is int teamId && teamId > 0
              ? await _context.TeamMembers.Where(tm => tm.TeamId == teamId && tm.Role == TeamMemberRole.LEADER).Select(tm => tm.UserId).FirstOrDefaultAsync(ct)
              : 0,
        ReportTargetType.User => targetId,
        ReportTargetType.Team => await _context.TeamMembers
            .Where(tm => tm.TeamId == targetId && tm.Role == TeamMemberRole.LEADER)
            .Select(tm => tm.UserId)
            .FirstOrDefaultAsync(ct),
        _ => 0
      };
    }
  }
}
