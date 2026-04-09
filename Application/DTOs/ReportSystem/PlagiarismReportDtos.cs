using Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.ReportSystem
{
  public class CreatePlagiarismReportRequest
  {
    [Required]
    public ReportTargetType TargetType { get; set; }

    [Required]
    public int TargetId { get; set; }

    [Required]
    public ReportReason Reason { get; set; }
    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public List<string> EvidenceUrls { get; set; } = new List<string>();
  }

  public class PlagiarismReportDto
  {
    public int ReportId { get; set; }
    public int ReporterId { get; set; }
    public string ReporterName { get; set; } = string.Empty;

    public ReportTargetType TargetType { get; set; }
    public int TargetId { get; set; }
    public string TargetName { get; set; } = string.Empty;
    public ReportReason Reason { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> EvidenceUrls { get; set; } = new List<string>();

    public ReportStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
  }

  public class ResolvePlagiarismReportRequest
  {
    [Required]
    public ReportStatus NewStatus { get; set; }
    public string? ResolutionNotes { get; set; }

    public bool StrikeContent { get; set; }

    public int? PenaltyScore { get; set; }

    public bool BanCreator { get; set; }

    public bool SendWarning { get; set; }
  }

  public class CompareTranslationResponse
  {
    public CompareTranslationDetail Reported { get; set; } = new();
    public CompareTranslationDetail Reference { get; set; } = new();
  }

  public class CompareTranslationDetail
  {
    public int TranslationId { get; set; }
    public int ChapterId { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public List<string> ImageUrls { get; set; } = new List<string>();
  }
  public class PlagiarismReportStatsDto
  {
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Severe { get; set; }
    public int Resolved { get; set; }
  }
}
