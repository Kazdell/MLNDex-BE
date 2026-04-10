using Application.DTOs.Common;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth
{
  public class ChangePasswordDto
  {
    [Required(ErrorMessage = ErrorCodes.VALIDATION_ERROR)]
    public string CurrentPassword { get; set; } = null!;

    [Required(ErrorMessage = ErrorCodes.VALIDATION_ERROR)]
    [MinLength(6, ErrorMessage = ErrorCodes.VALIDATION_ERROR)]
    public string NewPassword { get; set; } = null!;
  }
}

