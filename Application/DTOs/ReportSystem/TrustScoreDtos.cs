using System;
using System.Collections.Generic;
using Domain.Entities;
using Domain.Enums;

namespace Application.DTOs.ReportSystem
{
  // ── Admin Restore ────────────────────────────────────
  public class RestoreTrustScoreRequest
  {
    public TrustScoreTargetType TargetType { get; set; }
    public int TargetId { get; set; }
    public int ScoreToRestore { get; set; }
    public string Reason { get; set; } = string.Empty;
  }

	public enum TrustScoreTargetType
	{
		User,
		Team
	}

	public class TrustScoreRestoreResultDto
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
