using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Auth
{
  public class FacebookLoginDto
  {
    [Required]
    public string AccessToken { get; set; } = null!;  // Facebook
  }
}
