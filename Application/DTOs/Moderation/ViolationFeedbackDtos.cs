using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Moderation
{
  public class ViolationFeedbackRequest
  {
    [Required]
    [StringLength(500)]
    public string Message { get; set; } = string.Empty;
  }

  public class ViolationFeedbackDto
  {
    public int QueueId { get; set; }
    public int TargetContentId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
  }
}
