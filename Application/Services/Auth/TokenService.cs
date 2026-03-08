using Application.Interfaces.Auth;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services.Auth
{
	public class TokenService : ITokenService
	{
		private readonly IConfiguration _config;
		private readonly IMemoryCache _cache;

		public TokenService(IConfiguration config, IMemoryCache cache)
		{
			_config = config;
			_cache = cache;
		}

		public string GenerateJwtToken(Domain.Entities.User user)
		{
			var roles = user.UserRoles.Select(ur => ur.Role.RoleName.ToString()).ToList();

			var claims = new List<Claim>
		{
			new Claim("id", user.UserId.ToString()),
			new Claim("email", user.Email),
			new Claim("username", user.Username),
			new Claim("displayName", user.DisplayName ?? user.Username),
		};

			foreach (var role in roles)
				claims.Add(new Claim("role", role));

			var key = new SymmetricSecurityKey(
				Encoding.UTF8.GetBytes(_config["JwtSettings:securitykey"]!));
			var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
			var expires = DateTime.UtcNow.AddMinutes(
				Convert.ToDouble(_config["JwtSettings:AccessTokenExpiryMinutes"]));

			var token = new JwtSecurityToken(
				issuer: _config["JwtSettings:Issuer"],
				audience: _config["JwtSettings:Audience"],
				claims: claims,
				expires: expires,
				signingCredentials: creds
			);

			return new JwtSecurityTokenHandler().WriteToken(token);
		}

		public void BlacklistToken(string token, DateTime expiry)
		{
			var remaining = expiry - DateTime.UtcNow;
			if (remaining > TimeSpan.Zero)
				_cache.Set($"blacklist:{token}", true, remaining);
		}

		public bool IsTokenBlacklisted(string token)
			=> _cache.TryGetValue($"blacklist:{token}", out _);
	}
}
