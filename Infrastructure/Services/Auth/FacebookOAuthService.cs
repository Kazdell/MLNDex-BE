using Application.DTOs.Auth;
using Application.Interfaces.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Infrastructure.Services.Auth
{
  public class FacebookOAuthService : IFacebookOAuthService
  {
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<FacebookOAuthService> _logger;

    public FacebookOAuthService(IConfiguration config, HttpClient httpClient, ILogger<FacebookOAuthService> logger)
    {
      _config = config;
      _httpClient = httpClient;
      _logger = logger;
    }

    public async Task<SocialUserInfoDto?> VerifyTokenAsync(string accessToken)
    {
      try
      {
        var appId = _config["FacebookSettings:AppId"];
        var appSecret = _config["FacebookSettings:AppSecret"];

        _logger.LogInformation($"Verifying Facebook token...");

        // 1. Verify token
        var verifyUrl = $"https://graph.facebook.com/debug_token?input_token={accessToken}&access_token={appId}|{appSecret}";
        var verifyResponse = await _httpClient.GetFromJsonAsync<FacebookTokenResponse>(verifyUrl);

        _logger.LogInformation($"Facebook verify response: IsValid={verifyResponse?.Data?.IsValid}");

        if (verifyResponse?.Data == null || !verifyResponse.Data.IsValid)
          return null;

        // 2. Lấy thông tin user
        var userUrl = $"https://graph.facebook.com/me?fields=id,name,email&access_token={accessToken}";
        var userInfo = await _httpClient.GetFromJsonAsync<FacebookUserInfo>(userUrl);

        _logger.LogInformation($"Facebook user info: Id={userInfo?.Id}, Email={userInfo?.Email}");

        if (userInfo == null) return null;

        return new SocialUserInfoDto
        {
          SocialId = userInfo.Id,
          Email = userInfo.Email,
          Name = userInfo.Name
        };
      }
      catch (Exception ex)
      {
        _logger.LogError($"Facebook token validation failed: {ex.Message}");
        return null;
      }
    }
  }

  // Helper classes
  public class FacebookTokenResponse
  {
    [JsonPropertyName("data")]
    public FacebookTokenData? Data { get; set; }
  }

  public class FacebookTokenData
  {
    [JsonPropertyName("is_valid")]
    public bool IsValid { get; set; }
  }

  public class FacebookUserInfo
  {
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("email")]
    public string Email { get; set; } = null!;
  }
}
