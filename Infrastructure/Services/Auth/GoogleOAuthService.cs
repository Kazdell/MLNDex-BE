using Application.DTOs.Auth;
using Application.Interfaces.Auth;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Infrastructure.Services.Auth
{
  public class GoogleOAuthService : IGoogleOAuthService
  {
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleOAuthService> _logger;

    public GoogleOAuthService(IConfiguration config, HttpClient httpClient, ILogger<GoogleOAuthService> logger)
    {
      _config = config;
      _httpClient = httpClient;
      _logger = logger;
    }

    public async Task<SocialUserInfoDto?> VerifyTokenAsync(string token)
    {
      // Try 1: Validate as ID Token (from GoogleLogin component / credential flow)
      try
      {
        var clientId = _config["GoogleSettings:ClientId"];
        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
          Audience = new[] { clientId }
        };

        var payload = await GoogleJsonWebSignature.ValidateAsync(token, settings);
        _logger.LogInformation("Google ID token valid for email: {Email}", payload.Email);

        return new SocialUserInfoDto
        {
          SocialId = payload.Subject,
          Email = payload.Email,
          Name = payload.Name
        };
      }
      catch (Exception ex)
      {
        _logger.LogWarning("Google ID token validation failed, trying as access_token: {Message}", ex.Message);
      }

      // Try 2: Validate as Access Token (from useGoogleLogin implicit flow)
      try
      {
        var userInfo = await _httpClient.GetFromJsonAsync<GoogleUserInfoResponse>(
            $"https://www.googleapis.com/oauth2/v3/userinfo?access_token={token}");

        if (userInfo == null || string.IsNullOrEmpty(userInfo.Email))
        {
          _logger.LogError("Google access_token returned no user info");
          return null;
        }

        _logger.LogInformation("Google access_token valid for email: {Email}", userInfo.Email);

        return new SocialUserInfoDto
        {
          SocialId = userInfo.Sub,
          Email = userInfo.Email,
          Name = userInfo.Name
        };
      }
      catch (Exception ex)
      {
        _logger.LogError("Google access_token validation also failed: {Message}", ex.Message);
        return null;
      }
    }
  }

  public class GoogleUserInfoResponse
  {
    [JsonPropertyName("sub")]
    public string Sub { get; set; } = null!;

    [JsonPropertyName("email")]
    public string Email { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
  }
}
