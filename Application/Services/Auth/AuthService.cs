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

    public AuthService(
        IMlndexDbContext context,
        ITokenService tokenService,
        IOtpService otpService,
        IEmailService emailService,
        IConfiguration configuration)
    {
      _context = context;
      _tokenService = tokenService;
      _otpService = otpService;
      _emailService = emailService;
      _configuration = configuration;
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
  }
}
