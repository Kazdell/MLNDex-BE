using Application.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Auth
{
	public interface IGoogleOAuthService
	{
		Task<SocialUserInfoDto?> VerifyTokenAsync(string idToken);
	}
}
