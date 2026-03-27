using Application.DTOs.Auth;
using Application.Interfaces.Auth;
using Application.Interfaces.Common;
using Application.Interfaces.Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Application.Services.Auth
{
	public class AuthService : IAuthService
	{
		private readonly IMlndexDbContext _context;
		private readonly ITokenService _tokenService;
		private readonly IOtpService _otpService;
		private readonly IEmailService _emailService;
		private readonly IConfiguration _configuration;
		private readonly IGoogleOAuthService _googleOAuth;
		private readonly IFacebookOAuthService _facebookOAuth;

		public AuthService(
			IMlndexDbContext context,
			ITokenService tokenService,
			IOtpService otpService,
			IEmailService emailService,
			IConfiguration configuration,
			IGoogleOAuthService googleOAuth,
			IFacebookOAuthService facebookOAuth)
		{
			_context = context;
			_tokenService = tokenService;
			_otpService = otpService;
			_emailService = emailService;
			_configuration = configuration;
			_googleOAuth = googleOAuth;
			_facebookOAuth = facebookOAuth;
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

			await CreateDefaultUserDataAsync(user);

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
			// 1. Kiểm tra nếu là tài khoản Admin từ appsettings.json
			var adminUsername = _configuration["Admin:Username"];
			var adminEmail = _configuration["Admin:Email"];
			var adminPassword = _configuration["Admin:Password"];

			bool isAdminLogin = false;
			if (!string.IsNullOrEmpty(adminUsername) && !string.IsNullOrEmpty(adminPassword))
			{
				isAdminLogin = (dto.Username.Equals(adminUsername, StringComparison.OrdinalIgnoreCase) ||
								(!string.IsNullOrEmpty(adminEmail) && dto.Username.Equals(adminEmail, StringComparison.OrdinalIgnoreCase)))
								&& dto.Password == adminPassword;
			}

			var user = await _context.Users
				.Include(u => u.UserRoles)
					.ThenInclude(ur => ur.Role)
				.FirstOrDefaultAsync(u => u.Username == dto.Username.ToLower() || u.Email == dto.Username.ToLower());

			if (isAdminLogin)
			{
				// Nếu login đúng credential admin nhưng user chưa tồn tại trong DB, tạo mới
				if (user == null)
				{
					user = new Domain.Entities.User
					{
						Username = adminUsername!.ToLower(),
						Email = adminEmail!.ToLower(),
						DisplayName = "System Admin",
						PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
						IsEmailVerified = true,
						IsActive = true,
						CreatedAt = DateTime.UtcNow
					};
					_context.Users.Add(user);
					await _context.SaveChangesAsync();
				}

				// Đảm bảo Admin có role ADMIN
				var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == RoleName.ADMIN);
				if (adminRole != null && !user.UserRoles.Any(ur => ur.RoleId == adminRole.RoleId))
				{
					_context.UserRoles.Add(new UserRole
					{
						UserId = user.UserId,
						RoleId = adminRole.RoleId,
						AssignedAt = DateTime.UtcNow
					});
					await _context.SaveChangesAsync();

					// Re-fetch user to include the new role
					user = await _context.Users
						.Include(u => u.UserRoles)
							.ThenInclude(ur => ur.Role)
						.FirstAsync(u => u.UserId == user.UserId);
				}
			}
			else
			{
				// Login bình thường
				if (user == null) return null;
				if (!user.IsEmailVerified) return null;
				if (!user.IsActive) return null;
				if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash)) return null;
			}

			var token = _tokenService.GenerateJwtToken(user);
			var refreshToken = _tokenService.GenerateRefreshToken();

			user.RefreshToken = refreshToken;
			user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["JwtSettings:RefreshTokenExpiryDays"] ?? "7"));
			await _context.SaveChangesAsync();

			return new AuthResponseDto
			{
				AccessToken = token,
				RefreshToken = refreshToken,
				UserId = user.UserId,
				ExpiresAt = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration["JwtSettings:AccessTokenExpiryMinutes"] ?? "15")),
				Username = user.Username,
				DisplayName = user.DisplayName ?? user.Username,
				Email = user.Email,
				Roles = user.UserRoles.Select(ur => ur.Role.RoleName.ToString()).ToList(),
				CannotUpload = user.CannotUpload
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

		// ── REFRESH TOKEN ───────────────────────────────────
		public async Task<AuthResponseDto?> RefreshAsync(TokenApiDto dto)
		{
			if (string.IsNullOrEmpty(dto.RefreshToken))
				return null;

			var user = await _context.Users
				.Include(u => u.UserRoles)
					.ThenInclude(ur => ur.Role)
				.FirstOrDefaultAsync(u => u.RefreshToken == dto.RefreshToken);

			if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
				return null;

			var newAccessToken = _tokenService.GenerateJwtToken(user);
			var newRefreshToken = _tokenService.GenerateRefreshToken();

			user.RefreshToken = newRefreshToken;
			user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["JwtSettings:RefreshTokenExpiryDays"] ?? "7"));
			await _context.SaveChangesAsync();

			return new AuthResponseDto
			{
				AccessToken = newAccessToken,
				RefreshToken = newRefreshToken,
				UserId = user.UserId,
				ExpiresAt = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration["JwtSettings:AccessTokenExpiryMinutes"] ?? "15")),
				Username = user.Username,
				DisplayName = user.DisplayName ?? user.Username,
				Email = user.Email,
				Roles = user.UserRoles.Select(ur => ur.Role.RoleName.ToString()).ToList(),
				CannotUpload = user.CannotUpload
			};
		}

		// ── FORGOT PASSWORD ─────────────────────────────────
		public async Task<ServiceResult> ForgotPasswordAsync(string email)
		{
			var user = await _context.Users
				.FirstOrDefaultAsync(u => u.Email == email.ToLower());
			// Always return success to prevent email enumeration
			if (user == null || !user.IsEmailVerified || !user.IsActive)
				return ServiceResult.Ok("Nếu email tồn tại, chúng tôi đã gửi mã xác minh.");

			var otp = await _otpService.GenerateOtpAsync(email);
			await _emailService.SendOtpEmailAsync(email, otp);

			return ServiceResult.Ok("Mã xác minh đã được gửi đến email của bạn.");
		}

		// ── RESET PASSWORD ──────────────────────────────────
		public async Task<ServiceResult> ResetPasswordAsync(string email, string otpCode, string newPassword)
		{
			var valid = _otpService.ValidateOtp(email, otpCode);
			if (!valid)
				return ServiceResult.Fail("Mã OTP không hợp lệ hoặc đã hết hạn.");

			var user = await _context.Users
				.FirstOrDefaultAsync(u => u.Email == email.ToLower());
			if (user == null)
				return ServiceResult.Fail("Tài khoản không tồn tại.");

			user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
			// Invalidate existing refresh tokens
			user.RefreshToken = null;
			user.RefreshTokenExpiryTime = null;
			await _context.SaveChangesAsync();

			return ServiceResult.Ok("Đặt lại mật khẩu thành công. Vui lòng đăng nhập.");
		}

		// ── CHANGE PASSWORD (authenticated user) ─────────────
		public async Task<ServiceResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
		{
			var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
			if (user == null)
				return ServiceResult.Fail("Tài khoản không tồn tại.");

			// Verify current password
			if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
				return ServiceResult.Fail("Mật khẩu hiện tại không đúng.");

			// Prevent reusing the same password
			if (BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash))
				return ServiceResult.Fail("Mật khẩu mới không được trùng với mật khẩu hiện tại.");

			user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
			// Invalidate refresh token to force re-login on other devices
			user.RefreshToken = null;
			user.RefreshTokenExpiryTime = null;
			await _context.SaveChangesAsync();

			return ServiceResult.Ok("Đổi mật khẩu thành công.");
		}

		// ── GOOGLE LOGIN ─────────────────────────────────────────
		public async Task<AuthResponseDto?> GoogleLoginAsync(GoogleLoginDto dto)
		{
			// 1. Verify token với Google
			var socialUser = await _googleOAuth.VerifyTokenAsync(dto.IdToken);
			if (socialUser == null) return null;

			// 2. Tìm hoặc merge account
			var user = await FindOrMergeAccountAsync(socialUser.Email, socialUser.SocialId, null, socialUser.Name);
			if (user == null) return null;

			// 3. Trả về JWT
			var token = _tokenService.GenerateJwtToken(user);
			return new AuthResponseDto
			{
				AccessToken = token,
				ExpiresAt = DateTime.UtcNow.AddDays(30),
				Username = user.Username,
				Email = user.Email,
				Roles = user.UserRoles.Select(ur => ur.Role.RoleName.ToString()).ToList()
			};
		}

		// ── FACEBOOK LOGIN ───────────────────────────────────────
		public async Task<AuthResponseDto?> FacebookLoginAsync(FacebookLoginDto dto)
		{
			// 1. Verify token với Facebook
			var socialUser = await _facebookOAuth.VerifyTokenAsync(dto.AccessToken);
			if (socialUser == null) return null;

			// 2. Tìm hoặc merge account
			var user = await FindOrMergeAccountAsync(socialUser.Email, null, socialUser.SocialId, socialUser.Name);
			if (user == null) return null;

			// 3. Trả về JWT
			var token = _tokenService.GenerateJwtToken(user);
			return new AuthResponseDto
			{
				AccessToken = token,
				ExpiresAt = DateTime.UtcNow.AddDays(30),
				Username = user.Username,
				Email = user.Email,
				Roles = user.UserRoles.Select(ur => ur.Role.RoleName.ToString()).ToList()
			};
		}

		// ── ACCOUNT MERGE ────────────────────────────────────────
		private async Task<Domain.Entities.User?> FindOrMergeAccountAsync(
				string email, string? googleId, string? facebookId, string name)
		{
			var emailLower = email.ToLower();
			var user = await _context.Users
				.Include(u => u.UserRoles)
					.ThenInclude(ur => ur.Role)
				.FirstOrDefaultAsync(u => u.Email == emailLower);

			if (user != null)
			{
				// Account đã tồn tại → merge: gắn thêm socialId nếu chưa có
				// Primary account là account đăng ký trước (giữ nguyên, không thay đổi)
				if (googleId != null && user.GoogleId == null)
					user.GoogleId = googleId;

				if (facebookId != null && user.FacebookId == null)
					user.FacebookId = facebookId;

				// Đánh dấu đã verify vì social login đã xác thực email
				if (!user.IsEmailVerified)
					user.IsEmailVerified = true;

				await _context.SaveChangesAsync();
			}
			else
			{
				// Chưa có account → tạo mới qua social login
				var username = await GenerateUniqueUsernameAsync(name);

				user = new Domain.Entities.User
				{
					Email = emailLower,
					Username = username,
					DisplayName = name,
					GoogleId = googleId,
					FacebookId = facebookId,
					IsEmailVerified = true,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				};
				_context.Users.Add(user);
				await _context.SaveChangesAsync();

				await CreateDefaultUserDataAsync(user);

				// Load lại roles để GenerateJwtToken dùng
				// Load lại user kèm roles sau khi tạo mới
				user = await _context.Users
					.Include(u => u.UserRoles)
						.ThenInclude(ur => ur.Role)
					.FirstAsync(u => u.UserId == user.UserId);
			}

			return user;
		}

		// Tạo username unique từ tên social
		private async Task<string> GenerateUniqueUsernameAsync(string name)
		{
			// Chuyển name thành username hợp lệ: chỉ chữ, số, _
			var base_ = Regex.Replace(name.ToLower(), @"[^a-z0-9_]", "_");
			if (base_.Length < 3) base_ = base_.PadRight(3, '0');
			if (base_.Length > 15) base_ = base_[..15];

			var username = base_;
			var count = 1;

			// Đảm bảo unique
			while (await _context.Users.AnyAsync(u => u.Username == username))
			{
				username = $"{base_}{count++}";
			}

			return username;
		}

		private async Task CreateDefaultUserDataAsync(Domain.Entities.User user)
		{
			// Gán role READER
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
			}

			// Tạo Wallet mặc định
			_context.Wallets.Add(new Wallet
			{
				UserId = user.UserId,
				CoinBalance = 0,
				TotalEarned = 0,
				TotalSpent = 0
			});

			await _context.SaveChangesAsync();
		}
	}
}
