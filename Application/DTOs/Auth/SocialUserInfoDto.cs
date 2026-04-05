using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Auth
{
  public class SocialUserInfoDto
  {
    public string SocialId { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
  }
}
