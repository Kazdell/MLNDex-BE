using Application.DTOs.Auth;
using Application.Interfaces.Auth;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services.Auth
{
  public class GoogleOAuthService : IGoogleOAuthService
  {
    private readonly IConfiguration _config;
    private readonly ILogger<GoogleOAuthService> _logger;

    public GoogleOAuthService(IConfiguration config, ILogger<GoogleOAuthService> logger)
    {
      _config = config;
      _logger = logger;
    }

    public async Task<SocialUserInfoDto?> VerifyTokenAsync(string idToken)
    {
      try
      {
        var clientId = _config["GoogleSettings:ClientId"];
        _logger.LogInformation($"Verifying Google token with ClientId: {clientId}");

        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
          Audience = new[] { clientId }
        };

        var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        _logger.LogInformation($"Google token valid for email: {payload.Email}");

        return new SocialUserInfoDto
        {
          SocialId = payload.Subject,
          Email = payload.Email,
          Name = payload.Name
        };
      }
      catch (Exception ex)
      {
        _logger.LogError($"Google token validation failed: {ex.Message}");
        return null;
      }
    }
  }
}
