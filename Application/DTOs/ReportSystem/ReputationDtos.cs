using System;
using System.Collections.Generic;
using Domain.Entities;
using Domain.Enums;

namespace Application.DTOs.ReportSystem
{
  // ── Admin Restore ────────────────────────────────────
  public class RestoreReputationRequest
  {
    public ReputationTargetType TargetType { get; set; }
    public int TargetId { get; set; }
    public int ScoreToRestore { get; set; }
    public string Reason { get; set; } = string.Empty;
  }

  // Alias used by IsolatedReportsController for backward compatibility
  public class RestoreReputationScoreRequest : RestoreReputationRequest { }

	public enum ReputationTargetType
	{
		Creator,
		Team
	}

	public class ReputationRestoreResultDto
  {
    public string TargetName { get; set; } = string.Empty;
    public int OldScore { get; set; }
    public int NewScore { get; set; }
    public bool CanUpload { get; set; }
    public string Reason { get; set; } = string.Empty;
  }

  // ── Appeal System ────────────────────────────────────
  public class CreateAppealRequest
  {
    public int? RelatedReportId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceUrl { get; set; }
  }

  public class ReviewAppealRequest
  {
    public bool IsApproved { get; set; }
    public string? ReviewNotes { get; set; }
    public int? ScoreToRestore { get; set; } // Only used when approved
  }

  public class AppealDto
  {
    public int AppealId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int? RelatedReportId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReviewNotes { get; set; }
    public int? ScoreRestored { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
  }

  // ── Reputation History ────────────────────────────────
  public class ReputationHistoryDto
  {
    public int Id { get; set; }
    public int? CreatorId { get; set; }
    public int? TranslationTeamId { get; set; }
    public int ScoreChange { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int? RelatedReportId { get; set; }
    public DateTime CreatedAt { get; set; }
  }

  // ── Reputation Overview ────────────────────────────────
  public class ReputationOverviewDto
  {
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int CurrentScore { get; set; }
    public int HistoryCount { get; set; }
  }

  // ── Translation Portfolio ────────────────────────────
  public class UserTranslationHistoryDto
  {
    public int TranslationId { get; set; }
    public int SeriesId { get; set; }
    public int ChapterId { get; set; }
    public string SeriesTitle { get; set; } = string.Empty;
    public string ChapterTitle { get; set; } = string.Empty;
    public float ChapterNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
  }
}
