using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Entities;

namespace Application.Interfaces.Auth
{
	public interface ITokenService
	{
		string GenerateJwtToken(Domain.Entities.User user);
		string GenerateRefreshToken();
		bool IsTokenBlacklisted(string token);
		void BlacklistToken(string token, DateTime expiry);
	}
}
