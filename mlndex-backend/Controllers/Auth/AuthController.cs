using Application.DTOs.Auth;
using Application.Interfaces.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Auth
{
	[ApiController]
	[Route("api/auth")]
	public class AuthController : BaseController
	{
		private readonly IAuthService _authService;

		public AuthController(IAuthService authService)
		{
			_authService = authService;
		}

		// POST /api/auth/register
		[HttpPost("register")]
		public async Task<IActionResult> Register([FromBody] RegisterDto dto)
		{
			if (!ModelState.IsValid)
				return BadRequestResponse("Dữ liệu không hợp lệ.");

			var result = await _authService.RegisterAsync(dto);
			return result.Success
				? OkResponse<object>(null, result.Message)
				: BadRequestResponse(result.Message);
		}

		// POST /api/auth/verify-otp
		[HttpPost("verify-otp")]
		public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
		{
			if (!ModelState.IsValid)
				return BadRequestResponse("Dữ liệu không hợp lệ.");

			var result = await _authService.VerifyEmailOtpAsync(dto);
			return result.Success
				? OkResponse<object>(null, result.Message)
				: BadRequestResponse(result.Message);
		}

		// POST /api/auth/login
		[HttpPost("login")]
		public async Task<IActionResult> Login([FromBody] LoginDto dto)
		{
			if (!ModelState.IsValid)
				return BadRequestResponse("Dữ liệu không hợp lệ.");

			var result = await _authService.LoginAsync(dto);
			if (result == null)
				return UnauthorizedResponse("Email/password không đúng hoặc chưa xác thực email.");

			return OkResponse(result, "Đăng nhập thành công.");
		}

		// POST /api/auth/logout
		[HttpPost("logout")]
		[Authorize]
		public async Task<IActionResult> Logout()
		{
			var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
			if (string.IsNullOrEmpty(token))
				return BadRequestResponse("Token không hợp lệ.");

			var result = await _authService.LogoutAsync(token);
			return result.Success
				? OkResponse<object>(null, result.Message)
				: BadRequestResponse(result.Message);
		}
	}
}
