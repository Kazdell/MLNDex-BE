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
        throw new KeyNotFoundException("User không tồn tại.");

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

    public async Task<List<PlagiarismReportDto>> GetPendingReportsAsync(int page = 1, int limit = 20, CancellationToken cancellationToken = default)
    {
      var query = _context.Reports
          .Include(r => r.Reporter)
          .Where(r => r.Status == ReportStatus.Pending || r.Status == ReportStatus.Investigating)
          .OrderBy(r => r.CreatedAt);

      var reports = await query
          .Skip((page - 1) * limit)
          .Take(limit)
          .ToListAsync(cancellationToken);

      var result = new List<PlagiarismReportDto>();
      foreach (var r in reports)
      {
        var dto = MapToDto(r, r.Reporter.Username);
        dto.TargetName = await GetTargetNameAsync(r.ContentType, r.ContentId, cancellationToken);
        result.Add(dto);
      }

      return result;
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
        throw new KeyNotFoundException("Report không tồn tại.");

      if (report.Status == ReportStatus.Resolved || report.Status == ReportStatus.Rejected)
        throw new InvalidOperationException("Report đã được xử lý.");

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
      return dto;
    }

    public async Task<CompareTranslationResponse> GetCompareDataAsync(int reportId, int referenceTranslationId, CancellationToken cancellationToken = default)
    {
      var report = await _context.Reports.FindAsync(new object[] { reportId }, cancellationToken);
      if (report == null) throw new KeyNotFoundException("Report không tồn tại");

      if (report.ContentType != ReportTargetType.ChapterTranslation)
      {
        throw new InvalidOperationException("Tính năng compare chỉ hỗ trợ loại Report ChapterTranslation.");
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
        throw new KeyNotFoundException("Một trong hai bản dịch không tồn tại.");
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
            team.TrustScore -= score;
            if (team.TrustScore <= 0) team.LockStatus = TeamLockStatus.LOCKED;

            _context.TrustScoreHistories.Add(new TrustScoreHistory
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
          team.TrustScore -= score;
          if (team.TrustScore <= 0) team.LockStatus = TeamLockStatus.LOCKED;

          _context.TrustScoreHistories.Add(new TrustScoreHistory
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
        var user = await _context.Users.FindAsync(new object[] { report.ContentId }, ct);
        if (user != null)
        {
          user.TrustScore -= score;
          if (user.TrustScore <= 0) user.CannotUpload = true;

          _context.TrustScoreHistories.Add(new TrustScoreHistory
          {
            UserId = user.UserId,
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
