using Application.DTOs.Common;
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
        ErrorMessage = ErrorCodes.VALIDATION_ERROR)]
    public string Username { get; set; } = null!;

    [Required]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$",
        ErrorMessage = ErrorCodes.VALIDATION_ERROR)]
    public string Password { get; set; } = null!;
  }
}

