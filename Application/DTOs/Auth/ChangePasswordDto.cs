using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth
{
  public class ChangePasswordDto
  {
    [Required(ErrorMessage = "Mật khẩu hiện tại không được để trống.")]
    public string CurrentPassword { get; set; } = null!;

    [Required(ErrorMessage = "Mật khẩu mới không được để trống.")]
    [MinLength(6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.")]
    public string NewPassword { get; set; } = null!;
  }
}
