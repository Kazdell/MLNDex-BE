using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Community
{
  public class UpdateCommentStatusRequest
  {
    [Required]
    [RegularExpression("^(hide|delete|restore)$", ErrorMessage = "Action must be 'hide', 'delete', or 'restore'")]
    public string Action { get; set; } = string.Empty;
  }

  public class BulkUpdateCommentStatusRequest
  {
    [Required]
    [MinLength(1, ErrorMessage = "Phải chọn ít nhất 1 bình luận")]
    public List<int> CommentIds { get; set; } = new List<int>();

    [Required]
    [RegularExpression("^(hide|delete|restore)$", ErrorMessage = "Action must be 'hide', 'delete', or 'restore'")]
    public string Action { get; set; } = string.Empty;
  }
}
