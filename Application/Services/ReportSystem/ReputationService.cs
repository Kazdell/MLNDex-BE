using Application.DTOs.Common;
using Application.Exceptions;
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
  public class ReputationService : IReputationService
  {
    private readonly IMlndexDbContext _context;

    public ReputationService(IMlndexDbContext context)
    {
      _context = context;
    }

    // ══════════════════════════════════════════════════════
    // Phase A: Admin Manual Restore
    // ══════════════════════════════════════════════════════
    public async Task<ReputationRestoreResultDto> RestoreReputationAsync(
        RestoreReputationRequest request, int moderatorId, CancellationToken ct = default)
    {
      if (request.ScoreToRestore <= 0)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

      if (request.TargetType == ReputationTargetType.Creator)
      {
        var creator = await _context.CreatorProfiles
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CreatorId == request.TargetId, ct);
        if (creator == null) throw new AppException(ErrorCodes.USER_NOT_FOUND);

        int oldScore = creator.ReputationScore;
        creator.ReputationScore += request.ScoreToRestore;

        // Cap at 100
        if (creator.ReputationScore > 100) creator.ReputationScore = 100;

        // Unblock upload if score is above 0
        if (creator.ReputationScore > 0 && creator.User != null) creator.User.CannotUpload = false;

        _context.ReputationHistories.Add(new ReputationHistory
        {
          CreatorId = creator.CreatorId,
          ScoreChange = request.ScoreToRestore,
          Reason = $"[Admin Restore] {request.Reason}",
          CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(ct);

        return new ReputationRestoreResultDto
        {
          TargetName = creator.PenName,
          OldScore = oldScore,
          NewScore = creator.ReputationScore,
          CanUpload = creator.User == null ? true : !creator.User.CannotUpload,
          Reason = request.Reason
        };
      }
      else // Team
      {
        var team = await _context.TranslationTeams.FindAsync(new object[] { request.TargetId }, ct);
        if (team == null) throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.TEAM_NOT_FOUND);

        int oldScore = team.ReputationScore;
        team.ReputationScore += request.ScoreToRestore;
        if (team.ReputationScore > 100) team.ReputationScore = 100;

        // Unblock team if score is above 0
        if (team.ReputationScore > 0 && team.LockStatus == TeamLockStatus.LOCKED)
          team.LockStatus = TeamLockStatus.ACTIVE;

        _context.ReputationHistories.Add(new ReputationHistory
        {
          TranslationTeamId = team.TeamId,
          ScoreChange = request.ScoreToRestore,
          Reason = $"[Admin Restore] {request.Reason}",
          CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(ct);

        return new ReputationRestoreResultDto
        {
          TargetName = team.TeamName,
          OldScore = oldScore,
          NewScore = team.ReputationScore,
          CanUpload = team.LockStatus == TeamLockStatus.ACTIVE,
          Reason = request.Reason
        };
      }
    }

    // ══════════════════════════════════════════════════════
    // Alias: RestoreReputationScoreAsync (maps to RestoreReputationAsync)
    // ══════════════════════════════════════════════════════
    public Task<ReputationRestoreResultDto> RestoreReputationScoreAsync(
        RestoreReputationRequest request, int moderatorId, CancellationToken ct = default)
        => RestoreReputationAsync(request, moderatorId, ct);

    // ══════════════════════════════════════════════════════
    // Core: ModifyReputationAsync (+/- points + log history)
    // ══════════════════════════════════════════════════════
    public async Task ModifyReputationAsync(
        ReputationTargetType targetType,
        int targetId,
        int scoreChange,
        string reason,
        int? relatedReportId = null,
        CancellationToken ct = default)
    {
      if (targetType == ReputationTargetType.Creator)
      {
        var creator = await _context.CreatorProfiles
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CreatorId == targetId, ct)
            ?? throw new AppException(ErrorCodes.USER_NOT_FOUND);

        creator.ReputationScore = Math.Clamp(creator.ReputationScore + scoreChange, 0, 100);

        // Unblock/block upload based on score
        if (creator.User != null)
          creator.User.CannotUpload = creator.ReputationScore <= 0;

        _context.ReputationHistories.Add(new ReputationHistory
        {
          CreatorId = creator.CreatorId,
          ScoreChange = scoreChange,
          Reason = reason,
          RelatedReportId = relatedReportId,
          CreatedAt = DateTime.UtcNow
        });
      }
      else
      {
        var team = await _context.TranslationTeams
            .FindAsync(new object[] { targetId }, ct)
            ?? throw new AppException(ErrorCodes.TEAM_NOT_FOUND);

        team.ReputationScore = Math.Clamp(team.ReputationScore + scoreChange, 0, 100);

        // Lock team if score drops to 0
        if (team.ReputationScore <= 0)
          team.LockStatus = TeamLockStatus.LOCKED;
        else if (team.LockStatus == TeamLockStatus.LOCKED && team.ReputationScore > 0)
          team.LockStatus = TeamLockStatus.ACTIVE;

        _context.ReputationHistories.Add(new ReputationHistory
        {
          TranslationTeamId = team.TeamId,
          ScoreChange = scoreChange,
          Reason = reason,
          RelatedReportId = relatedReportId,
          CreatedAt = DateTime.UtcNow
        });
      }

      await _context.SaveChangesAsync(ct);
    }

    // ══════════════════════════════════════════════════════
    // Get Reputation History (paginated)
    // ══════════════════════════════════════════════════════
    public async Task<List<ReputationHistoryDto>> GetReputationHistoryAsync(
        int? creatorId,
        int? teamId,
        int page = 1,
        int limit = 20,
        CancellationToken ct = default)
    {
      var query = _context.ReputationHistories.AsQueryable();

      if (creatorId.HasValue)
        query = query.Where(h => h.CreatorId == creatorId);

      if (teamId.HasValue)
        query = query.Where(h => h.TranslationTeamId == teamId);

      var items = await query
          .OrderByDescending(h => h.CreatedAt)
          .Skip((page - 1) * limit)
          .Take(limit)
          .ToListAsync(ct);

      return items.Select(h => new ReputationHistoryDto
      {
        Id = h.Id,
        CreatorId = h.CreatorId,
        TranslationTeamId = h.TranslationTeamId,
        ScoreChange = h.ScoreChange,
        Reason = h.Reason,
        RelatedReportId = h.RelatedReportId,
        CreatedAt = h.CreatedAt
      }).ToList();
    }

    // ══════════════════════════════════════════════════════
    // Phase C: Appeal System
    // ══════════════════════════════════════════════════════
    public async Task<AppealDto> CreateAppealAsync(int userId, CreateAppealRequest request, CancellationToken ct = default)
    {
      var user = await _context.Users.FindAsync(new object[] { userId }, ct);
      if (user == null) throw new AppException(ErrorCodes.USER_NOT_FOUND);

      // Check for existing pending appeal
      var existingAppeal = await _context.Appeals
          .AnyAsync(a => a.UserId == userId && a.Status == AppealStatus.Pending, ct);
      if (existingAppeal)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

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

      if (appeal == null) throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.APPEAL_NOT_FOUND);
      if (appeal.Status != AppealStatus.Pending)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

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
          // We need to restore to the CreatorProfile of the user who made the appeal.
          // Since Appeal currently tracks UserId, we find the CreatorId.
          var creator = await _context.CreatorProfiles.FirstOrDefaultAsync(c => c.UserId == appeal.UserId, ct);
          if (creator != null)
          {
            // Delegate to RestoreReputationAsync for consistency
            await RestoreReputationAsync(new RestoreReputationRequest
            {
              TargetType = ReputationTargetType.Creator,
              TargetId = creator.CreatorId,
              ScoreToRestore = scoreToRestore,
              Reason = $"Appeal #{appealId} approved: {request.ReviewNotes}"
            }, moderatorId, ct);
          }
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
                  .ThenInclude(p => p!.Team)
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

    // ══════════════════════════════════════════════════════
    // Admin Overview
    // ══════════════════════════════════════════════════════
    public async Task<PagedResult<ReputationOverviewDto>> GetReputationOverviewAsync(string type, string? search, int page = 1, int limit = 20, CancellationToken ct = default)
    {
      if (type == "creator")
      {
        var query = _context.CreatorProfiles
            .Include(c => c.ReputationHistories)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
          var s = search.ToLower();
          query = query.Where(c => c.PenName.ToLower().Contains(s));
        }

        int totalItems = await query.CountAsync(ct);

        var creators = await query
            .OrderByDescending(c => c.ReputationScore)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(c => new ReputationOverviewDto
            {
              Id = c.CreatorId,
              Name = c.PenName,
              Type = "creator",
              CurrentScore = c.ReputationScore,
              HistoryCount = c.ReputationHistories.Count
            })
            .ToListAsync(ct);

        return new PagedResult<ReputationOverviewDto>
        {
          Items = creators,
          TotalCount = totalItems,
          Page = page,
          PageSize = limit
        };
      }
      else
      {
        var query = _context.TranslationTeams
            .Include(t => t.ReputationHistories)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
          var s = search.ToLower();
          query = query.Where(t => t.TeamName.ToLower().Contains(s));
        }

        int totalItems = await query.CountAsync(ct);

        var teams = await query
            .OrderByDescending(t => t.ReputationScore)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(t => new ReputationOverviewDto
            {
              Id = t.TeamId,
              Name = t.TeamName,
              Type = "team",
              CurrentScore = t.ReputationScore,
              HistoryCount = t.ReputationHistories.Count
            })
            .ToListAsync(ct);

        return new PagedResult<ReputationOverviewDto>
        {
          Items = teams,
          TotalCount = totalItems,
          Page = page,
          PageSize = limit
        };
      }
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
