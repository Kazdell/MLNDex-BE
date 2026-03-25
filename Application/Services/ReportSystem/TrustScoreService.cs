using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.ReportSystem;
using Application.Interfaces.Data;
using Application.Interfaces.ReportSystem;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.ReportSystem
{
  public class TrustScoreService : ITrustScoreService
  {
    private readonly IMlndexDbContext _context;

    public TrustScoreService(IMlndexDbContext context)
    {
      _context = context;
    }

    // ══════════════════════════════════════════════════════
    // Phase A: Admin Manual Restore
    // ══════════════════════════════════════════════════════
    public async Task<TrustScoreRestoreResultDto> RestoreTrustScoreAsync(
        RestoreTrustScoreRequest request, int moderatorId, CancellationToken ct = default)
    {
      if (request.ScoreToRestore <= 0)
        throw new InvalidOperationException("Điểm phục hồi phải lớn hơn 0.");

      if (request.TargetType == TrustScoreTargetType.User)
      {
        var user = await _context.Users.FindAsync(new object[] { request.TargetId }, ct);
        if (user == null) throw new KeyNotFoundException("User không tồn tại.");

        int oldScore = user.TrustScore;
        user.TrustScore += request.ScoreToRestore;

        // Cap at 100
        if (user.TrustScore > 100) user.TrustScore = 100;

        // Unblock upload if score is above 0
        if (user.TrustScore > 0) user.CannotUpload = false;

        _context.TrustScoreHistories.Add(new TrustScoreHistory
        {
          UserId = user.UserId,
          ScoreChange = request.ScoreToRestore,
          Reason = $"[Admin Restore] {request.Reason}",
          CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(ct);

        return new TrustScoreRestoreResultDto
        {
          TargetName = user.Username,
          OldScore = oldScore,
          NewScore = user.TrustScore,
          CanUpload = !user.CannotUpload,
          Reason = request.Reason
        };
      }
      else // Team
      {
        var team = await _context.TranslationTeams.FindAsync(new object[] { request.TargetId }, ct);
        if (team == null) throw new KeyNotFoundException("Team không tồn tại.");

        int oldScore = team.TrustScore;
        team.TrustScore += request.ScoreToRestore;
        if (team.TrustScore > 100) team.TrustScore = 100;

        // Unblock team if score is above 0
        if (team.TrustScore > 0 && team.LockStatus == TeamLockStatus.LOCKED)
          team.LockStatus = TeamLockStatus.ACTIVE;

        _context.TrustScoreHistories.Add(new TrustScoreHistory
        {
          TranslationTeamId = team.TeamId,
          ScoreChange = request.ScoreToRestore,
          Reason = $"[Admin Restore] {request.Reason}",
          CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(ct);

        return new TrustScoreRestoreResultDto
        {
          TargetName = team.TeamName,
          OldScore = oldScore,
          NewScore = team.TrustScore,
          CanUpload = team.LockStatus == TeamLockStatus.ACTIVE,
          Reason = request.Reason
        };
      }
    }

    // ══════════════════════════════════════════════════════
    // Phase C: Appeal System
    // ══════════════════════════════════════════════════════
    public async Task<AppealDto> CreateAppealAsync(int userId, CreateAppealRequest request, CancellationToken ct = default)
    {
      var user = await _context.Users.FindAsync(new object[] { userId }, ct);
      if (user == null) throw new KeyNotFoundException("User không tồn tại.");

      // Check for existing pending appeal
      var existingAppeal = await _context.Appeals
          .AnyAsync(a => a.UserId == userId && a.Status == AppealStatus.Pending, ct);
      if (existingAppeal)
        throw new InvalidOperationException("Bạn đã có đơn kháng cáo đang chờ xử lý.");

      var appeal = new Appeal
      {
        UserId = userId,
        RelatedReportId = request.RelatedReportId,
        Reason = request.Reason,
        EvidenceUrl = request.EvidenceUrl,
        Status = AppealStatus.Pending,
        CreatedAt = DateTime.UtcNow
      };

      _context.Appeals.Add(appeal);
      await _context.SaveChangesAsync(ct);

      return MapAppealToDto(appeal, user.Username);
    }

    public async Task<AppealDto> ReviewAppealAsync(int appealId, int moderatorId, ReviewAppealRequest request, CancellationToken ct = default)
    {
      var appeal = await _context.Appeals
          .Include(a => a.User)
          .FirstOrDefaultAsync(a => a.AppealId == appealId, ct);

      if (appeal == null) throw new KeyNotFoundException("Appeal không tồn tại.");
      if (appeal.Status != AppealStatus.Pending)
        throw new InvalidOperationException("Appeal đã được xử lý.");

      appeal.ReviewedBy = moderatorId;
      appeal.ReviewedAt = DateTime.UtcNow;
      appeal.ReviewNotes = request.ReviewNotes;

      if (request.IsApproved)
      {
        appeal.Status = AppealStatus.Approved;
        int scoreToRestore = request.ScoreToRestore ?? 0;
        appeal.ScoreRestored = scoreToRestore;

        if (scoreToRestore > 0)
        {
          // Delegate to RestoreTrustScoreAsync for consistency
          await RestoreTrustScoreAsync(new RestoreTrustScoreRequest
          {
            TargetType = TrustScoreTargetType.User,
            TargetId = appeal.UserId,
            ScoreToRestore = scoreToRestore,
            Reason = $"Appeal #{appealId} approved: {request.ReviewNotes}"
          }, moderatorId, ct);
        }
      }
      else
      {
        appeal.Status = AppealStatus.Rejected;
      }

      await _context.SaveChangesAsync(ct);
      return MapAppealToDto(appeal, appeal.User.Username);
    }

    public async Task<List<AppealDto>> GetPendingAppealsAsync(int page = 1, int limit = 20, CancellationToken ct = default)
    {
      var appeals = await _context.Appeals
          .Include(a => a.User)
          .Where(a => a.Status == AppealStatus.Pending)
          .OrderBy(a => a.CreatedAt)
          .Skip((page - 1) * limit)
          .Take(limit)
          .ToListAsync(ct);

      return appeals.Select(a => MapAppealToDto(a, a.User.Username)).ToList();
    }

    // ══════════════════════════════════════════════════════
    // Phase E: Translation Portfolio
    // ══════════════════════════════════════════════════════
    public async Task<List<UserTranslationHistoryDto>> GetUserTranslationHistoryAsync(int userId, CancellationToken ct = default)
    {
      var credits = await _context.TranslationCredits
          .Where(tc => tc.UserId == userId)
          .Include(tc => tc.Translation)
              .ThenInclude(t => t.Chapter)
                  .ThenInclude(c => c.Series)
          .Include(tc => tc.Translation)
              .ThenInclude(t => t.Permission)
                  .ThenInclude(p => p.Team)
          .OrderByDescending(tc => tc.Translation.PublishedAt)
          .ToListAsync(ct);

      return credits.Select(tc => new UserTranslationHistoryDto
      {
        TranslationId = tc.TranslationId,
        SeriesId = tc.Translation?.Chapter?.Series?.SeriesId ?? 0,
        ChapterId = tc.Translation?.Chapter?.ChapterId ?? 0,
        SeriesTitle = tc.Translation?.Chapter?.Series?.Title ?? "Unknown",
        ChapterTitle = tc.Translation?.Chapter?.Title ?? "Unknown",
        ChapterNumber = tc.Translation?.Chapter?.ChapterNumber ?? 0,
        Role = tc.Role.ToString(),
        TeamName = tc.Translation?.Permission?.Team?.TeamName ?? "Unknown",
        PublishedAt = tc.Translation?.PublishedAt
      }).ToList();
    }

    // ── Mapper ──────────────────────────────────────────
    private static AppealDto MapAppealToDto(Appeal appeal, string userName)
    {
      return new AppealDto
      {
        AppealId = appeal.AppealId,
        UserId = appeal.UserId,
        UserName = userName,
        RelatedReportId = appeal.RelatedReportId,
        Reason = appeal.Reason,
        EvidenceUrl = appeal.EvidenceUrl,
        Status = appeal.Status.ToString(),
        ReviewNotes = appeal.ReviewNotes,
        ScoreRestored = appeal.ScoreRestored,
        CreatedAt = appeal.CreatedAt,
        ReviewedAt = appeal.ReviewedAt
      };
    }
  }
}
