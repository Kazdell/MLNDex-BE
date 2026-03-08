using Application.DTOs.Auth;
using Application.Interfaces.Auth;
using Application.Interfaces.Common;
using Application.Interfaces.Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services.Auth
{
	public class AuthService : IAuthService
	{
		private readonly IMlndexDbContext _context;
		private readonly ITokenService _tokenService;
		private readonly IOtpService _otpService;
		private readonly IEmailService _emailService;

		public AuthService(
			IMlndexDbContext context,
			ITokenService tokenService,
			IOtpService otpService,
			IEmailService emailService)
		{
			_context = context;
			_tokenService = tokenService;
			_otpService = otpService;
			_emailService = emailService;
		}

		// ── REGISTER ────────────────────────────────────────
		public async Task<ServiceResult> RegisterAsync(RegisterDto dto)
		{
			// 1. Kiểm tra email trùng
			var existingEmail = await _context.Users
				.AnyAsync(u => u.Email == dto.Email.ToLower());
			if (existingEmail)
				return ServiceResult.Fail("Email đã được sử dụng.");

			// 2. Kiểm tra username trùng (case-insensitive)
			var existingUsername = await _context.Users
				.AnyAsync(u => u.Username == dto.Username.ToLower());
			if (existingUsername)
				return ServiceResult.Fail("Username đã tồn tại.");

			// 3. Hash password
			var hash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

			// 4. Tạo user chưa verify
			var user = new Domain.Entities.User
			{
				Email = dto.Email.ToLower(),
				Username = dto.Username.ToLower(),
				DisplayName = dto.Username,
				PasswordHash = hash,
				IsEmailVerified = false,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			};
			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			// 5. Gán role READER mặc định
			var readerRole = await _context.Roles
				.FirstOrDefaultAsync(r => r.RoleName == RoleName.READER);
			if (readerRole != null)
			{
				_context.UserRoles.Add(new UserRole
				{
					UserId = user.UserId,
					RoleId = readerRole.RoleId,
					AssignedAt = DateTime.UtcNow
				});
				await _context.SaveChangesAsync();
			}

			// 6. Gửi OTP
			var otp = await _otpService.GenerateOtpAsync(dto.Email);
			await _emailService.SendOtpEmailAsync(dto.Email, otp);

			return ServiceResult.Ok("Đăng ký thành công. Vui lòng kiểm tra email để xác thực.");
		}

		// ── VERIFY OTP ──────────────────────────────────────
		public async Task<ServiceResult> VerifyEmailOtpAsync(VerifyOtpDto dto)
		{
			var valid = _otpService.ValidateOtp(dto.Email, dto.Code);
			if (!valid)
				return ServiceResult.Fail("Mã OTP không hợp lệ hoặc đã hết hạn.");

			var user = await _context.Users
				.FirstOrDefaultAsync(u => u.Email == dto.Email.ToLower());
			if (user == null)
				return ServiceResult.Fail("Tài khoản không tồn tại.");

			user.IsEmailVerified = true;
			await _context.SaveChangesAsync();

			return ServiceResult.Ok("Xác thực email thành công. Bạn có thể đăng nhập.");
		}

		// ── LOGIN ───────────────────────────────────────────
		public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
		{
			var user = await _context.Users
				.Include(u => u.UserRoles)
					.ThenInclude(ur => ur.Role)
				.FirstOrDefaultAsync(u => u.Username == dto.Username.ToLower());

			if (user == null) return null;
			if (!user.IsEmailVerified) return null;
			if (!user.IsActive) return null;
			if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash)) return null;

			var token = _tokenService.GenerateJwtToken(user);

			return new AuthResponseDto
			{
				AccessToken = token,
				ExpiresAt = DateTime.UtcNow.AddDays(1),
				Username = user.Username,
				Email = user.Email,
				Roles = user.UserRoles.Select(ur => ur.Role.RoleName.ToString()).ToList()
			};
		}

		// ── LOGOUT ──────────────────────────────────────────
		public Task<ServiceResult> LogoutAsync(string token)
		{
			var handler = new JwtSecurityTokenHandler();
			var jwt = handler.ReadJwtToken(token);
			_tokenService.BlacklistToken(token, jwt.ValidTo);

			return Task.FromResult(ServiceResult.Ok("Đăng xuất thành công."));
		}
	}
}
