using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth
{
  public class ForgotPasswordDto
  {
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;
  }

  public class ResetPasswordDto
  {
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string OtpCode { get; set; } = null!;

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = null!;
  }
}
