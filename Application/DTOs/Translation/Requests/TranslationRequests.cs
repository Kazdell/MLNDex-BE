using System.Collections.Generic;
using Domain.Entities;

namespace Application.DTOs.Translation.Requests
{
  // Upload a new translation for a chapter
  public class UploadTranslationRequest
  {
    public int ChapterId { get; set; }
    public int? PermissionId { get; set; }
    public int? TeamId { get; set; }
    public int LanguageId { get; set; }
    public ContentType ContentType { get; set; }
    public List<Application.DTOs.Chapter.UploadPageDto>? Pages { get; set; }
    public string? ContentText { get; set; }
    public int? WordCount { get; set; }
    public List<TranslationCreditItem>? Credits { get; set; }
    public List<int>? JointTeamIds { get; set; }
  }

  // Credit entry for a translation contributor
  public class TranslationCreditItem
  {
    public int UserId { get; set; }
    public TranslationRole Role { get; set; }
  }

  // Edit an existing translation
  public class EditTranslationRequest
  {
    public int LanguageId { get; set; }
    public List<string>? ImageUrls { get; set; }
    public string? ContentUrl { get; set; }
    public int? WordCount { get; set; }
  }

  // Request translation permission from a series creator
  public class RequestPermissionRequest
  {
    public int SeriesId { get; set; }
    public int TeamId { get; set; }
    public int LanguageId { get; set; }
    public string? Note { get; set; }
    public bool IsUnofficial { get; set; }
  }

  // Review (approve/reject) a translation permission request
  public class ReviewPermissionRequest
  {
    public bool IsApproved { get; set; }
  }
}
