using System;
using System.Collections.Generic;
using Domain.Entities;

namespace Application.DTOs.Translation
{
  public class UploadTranslationDto
  {
    public int ChapterId { get; set; }
    public int PermissionId { get; set; }
    public int LanguageId { get; set; }
    public ContentType ContentType { get; set; } // IMAGE or TEXT
    public List<Application.DTOs.Chapter.UploadPageDto>? Pages { get; set; } // If Manga
    public string? ContentText { get; set; } // If Light Novel
    public int? WordCount { get; set; } // For Light Novel

    public List<TranslationCreditDto>? Credits { get; set; }
    public List<int>? JointTeamIds { get; set; }
  }

  public class TranslationCreditDto
  {
    public int UserId { get; set; }
    public TranslationRole Role { get; set; }
  }

  public class EditTranslationDto
  {
    public int LanguageId { get; set; }
    // Optionally update content...
    public List<string>? ImageUrls { get; set; }
    public string? ContentUrl { get; set; }
    public int? WordCount { get; set; }
  }

  public class TranslationDto
  {
    public int TranslationId { get; set; }
    public int ChapterId { get; set; }
    public int LanguageId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string QualityStatus { get; set; } = string.Empty;
    public string ModerationStatus { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public bool IsOfficial { get; set; }
    public bool IsOutdated { get; set; }
    public bool IsOrphan { get; set; }
    public List<string>? Pages { get; set; }
    public string? TextContent { get; set; }
  }
}
