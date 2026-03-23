using System;

namespace Application.DTOs.Auth
{
  public class TokenApiDto
  {
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
  }
}
