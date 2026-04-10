using Application.DTOs.Common;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Community
{
  public class UpdateCommentStatusRequest
  {
    [Required]
    [RegularExpression("^(hide|delete|restore)$", ErrorMessage = ErrorCodes.VALIDATION_ERROR)]
    public string Action { get; set; } = string.Empty;
  }

  public class BulkUpdateCommentStatusRequest
  {
    [Required]
    [MinLength(1, ErrorMessage = ErrorCodes.VALIDATION_ERROR)]
    public List<int> CommentIds { get; set; } = new List<int>();

    [Required]
    [RegularExpression("^(hide|delete|restore)$", ErrorMessage = ErrorCodes.VALIDATION_ERROR)]
    public string Action { get; set; } = string.Empty;
  }
}

