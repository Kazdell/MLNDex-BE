using Application.DTOs.Auth;
using Application.Interfaces.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Auth
{
	public interface IAuthService
	{
		Task<ServiceResult> RegisterAsync(RegisterDto dto);
		Task<ServiceResult> VerifyEmailOtpAsync(VerifyOtpDto dto);
		Task<AuthResponseDto?> LoginAsync(LoginDto dto);
		Task<AuthResponseDto?> RefreshAsync(TokenApiDto tokenApiDto);
		Task<ServiceResult> LogoutAsync(string token);
	}
}
