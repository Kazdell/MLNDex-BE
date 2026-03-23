using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Auth
{
  public class RegisterDto
  {
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [RegularExpression(@"^[a-zA-Z0-9_]{3,20}$",
        ErrorMessage = "Username chỉ được chứa chữ, số và dấu _, độ dài 3-20 ký tự")]
    public string Username { get; set; } = null!;

    [Required]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$",
        ErrorMessage = "Password cần ít nhất 8 ký tự, 1 hoa, 1 thường, 1 số, 1 ký tự đặc biệt")]
    public string Password { get; set; } = null!;
  }
}
