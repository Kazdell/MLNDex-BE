using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Domain.Enums;

namespace Application.DTOs.ReportSystem
{
  public class CreatePlagiarismReportRequest
  {
    [Required]
    public ReportTargetType TargetType { get; set; }

    [Required]
    public int TargetId { get; set; }

    [Required]
    public ReportReason Reason { get; set; } // Plagiarism, MTL_Abuse...

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
    public string TargetName { get; set; } = string.Empty; // Tên truyện hoặc tên chương

    public ReportReason Reason { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> EvidenceUrls { get; set; } = new List<string>();

    public ReportStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
  }

  public class ResolvePlagiarismReportRequest
  {
    [Required]
    public ReportStatus NewStatus { get; set; } // Resolved, Rejected

    public string? ResolutionNotes { get; set; }

    public bool StrikeContent { get; set; } // Nếu true, ẩn nội dung bị báo cáo

    public int? PenaltyScore { get; set; } // Trừ bao nhiêu điểm trust score
  }

  // DTO cho chức năng Side-by-Side Compare
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
}
