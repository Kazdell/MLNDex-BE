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
				? OkResponse<object?>(null, result.Message)
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
				? OkResponse<object?>(null, result.Message)
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
				? OkResponse<object?>(null, result.Message)
				: BadRequestResponse(result.Message);
		}

    // POST /api/auth/google
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto dto)
    {
      if (!ModelState.IsValid)
        return BadRequestResponse("Dữ liệu không hợp lệ.");

      var result = await _authService.GoogleLoginAsync(dto);
      if (result == null)
        return UnauthorizedResponse("Google token không hợp lệ.");

      return OkResponse(result, "Đăng nhập Google thành công.");
    }

    // POST /api/auth/facebook
    [HttpPost("facebook")]
    public async Task<IActionResult> FacebookLogin([FromBody] FacebookLoginDto dto)
    {
      if (!ModelState.IsValid)
        return BadRequestResponse("Dữ liệu không hợp lệ.");

      var result = await _authService.FacebookLoginAsync(dto);
      if (result == null)
        return UnauthorizedResponse("Facebook token không hợp lệ.");

      return OkResponse(result, "Đăng nhập Facebook thành công.");
    }

    // POST /api/auth/refresh
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] TokenApiDto dto)
    {
      if (dto == null) return BadRequestResponse("Dữ liệu không hợp lệ.");

      var result = await _authService.RefreshAsync(dto);
      if (result == null)
        return UnauthorizedResponse("Refresh token không hợp lệ hoặc đã hết hạn.");

      return OkResponse(result, "Token đã được cấp mới.");
    }

    // POST /api/auth/forgot-password — send OTP to email
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
      if (string.IsNullOrWhiteSpace(dto.Email))
        return BadRequestResponse("Email không được để trống.");
      var result = await _authService.ForgotPasswordAsync(dto.Email);
      return OkResponse<object?>(null, result.Message);
    }

    // POST /api/auth/reset-password — verify OTP and set new password
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
      if (!ModelState.IsValid)
        return BadRequestResponse("Dữ liệu không hợp lệ.");
      var result = await _authService.ResetPasswordAsync(dto.Email, dto.OtpCode, dto.NewPassword);
      return result.Success
          ? OkResponse<object?>(null, result.Message)
          : BadRequestResponse(result.Message);
    }

    // POST /api/auth/change-password
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
      if (!ModelState.IsValid)
        return BadRequestResponse("Dữ liệu không hợp lệ.");

      var userId = GetUserId();
      if (userId == 0) return UnauthorizedResponse("Chưa đăng nhập.");

      var result = await _authService.ChangePasswordAsync(userId, dto.CurrentPassword, dto.NewPassword);
      return result.Success
          ? OkResponse<object?>(null, result.Message)
          : BadRequestResponse(result.Message);
    }
  }
}
