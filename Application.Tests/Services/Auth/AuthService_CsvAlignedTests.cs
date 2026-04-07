using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.DTOs.Auth;
using Application.Interfaces.Auth;
using Application.Interfaces.Common;
using Application.Services.Auth;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.Auth;

public class AuthService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;
  private readonly Mock<ITokenService> _mockTokenService = new();
  private readonly Mock<IOtpService> _mockOtpService = new();
  private readonly Mock<IEmailService> _mockEmailService = new();
  private readonly Mock<IGoogleOAuthService> _mockGoogleOAuth = new();
  private readonly Mock<IFacebookOAuthService> _mockFacebookOAuth = new();

  public AuthService_CsvAlignedTests(ITestOutputHelper output)
  {
    _output = output;
  }

  private static MlndexDbContext CreateInMemoryDbContext()
  {
    var options = new DbContextOptionsBuilder<MlndexDbContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString())
      .Options;
    return new MlndexDbContext(options);
  }

  private static IConfiguration CreateConfiguration()
  {
    var values = new Dictionary<string, string?>
    {
      ["Admin:Username"] = "admin",
      ["Admin:Email"] = "admin@mlndex.local",
      ["Admin:Password"] = "Admin@123",
      ["JwtSettings:RefreshTokenExpiryDays"] = "7",
      ["JwtSettings:AccessTokenExpiryMinutes"] = "15"
    };

    return new ConfigurationBuilder()
      .AddInMemoryCollection(values)
      .Build();
  }

  private AuthService CreateService(MlndexDbContext db)
  {
    _mockTokenService
      .Setup(x => x.GenerateJwtToken(It.IsAny<User>()))
      .Returns("access-token");

    _mockTokenService
      .Setup(x => x.GenerateRefreshToken())
      .Returns("refresh-token");

    _mockTokenService
      .Setup(x => x.BlacklistToken(It.IsAny<string>(), It.IsAny<DateTime>()));

    _mockOtpService
      .Setup(x => x.GenerateOtpAsync(It.IsAny<string>()))
      .ReturnsAsync("123456");

    _mockOtpService
      .Setup(x => x.ValidateOtp(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(true);

    _mockEmailService
      .Setup(x => x.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
      .Returns(Task.CompletedTask);

    return new AuthService(
      db,
      _mockTokenService.Object,
      _mockOtpService.Object,
      _mockEmailService.Object,
      CreateConfiguration(),
      _mockGoogleOAuth.Object,
      _mockFacebookOAuth.Object);
  }

  private static async Task SeedRolesAsync(MlndexDbContext db)
  {
    db.Roles.AddRange(
      new Role { RoleId = 1, RoleName = RoleName.READER },
      new Role { RoleId = 2, RoleName = RoleName.ADMIN });
    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task RegisterAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);
    var service = CreateService(db);

    var input = new RegisterDto
    {
      Email = "newuser@test.com",
      Username = "new_user",
      Password = "Aa@12345"
    };

    var output = await service.RegisterAsync(input);

    _output.WriteLine($"Input: Email={input.Email}, Username={input.Username}");
    _output.WriteLine($"Output: Success={output.Success}, Message={output.Message}");

    output.Success.Should().BeTrue();
    output.Message.Should().Be("Đăng ký thành công. Vui lòng kiểm tra email để xác thực.");

    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "newuser@test.com");
    user.Should().NotBeNull();
    user!.IsEmailVerified.Should().BeFalse();

    var hasReaderRole = await db.UserRoles.AnyAsync(ur => ur.UserId == user.UserId && ur.RoleId == 1);
    hasReaderRole.Should().BeTrue();

    _mockOtpService.Verify(x => x.GenerateOtpAsync("newuser@test.com"), Times.Once);
    _mockEmailService.Verify(x => x.SendOtpEmailAsync("newuser@test.com", "123456"), Times.Once);
  }

  [Fact]
  public async Task RegisterAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);
    db.Users.Add(new User
    {
      UserId = 10,
      Email = "taken@test.com",
      Username = "existing",
      DisplayName = "Existing",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345")
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);

    var output = await service.RegisterAsync(new RegisterDto
    {
      Email = "taken@test.com",
      Username = "another_name",
      Password = "Aa@12345"
    });

    _output.WriteLine("Input: duplicate email=taken@test.com");
    _output.WriteLine($"Output: Success={output.Success}, Message={output.Message}");

    output.Success.Should().BeFalse();
    output.Message.Should().Be("Email đã được sử dụng.");
  }

  [Fact]
  public async Task RegisterAsync_TC03_InvalidInput_DuplicateUsernameCaseInsensitive()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);
    db.Users.Add(new User
    {
      UserId = 11,
      Email = "unique@test.com",
      Username = "existing_name",
      DisplayName = "Existing Name",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345")
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.RegisterAsync(new RegisterDto
    {
      Email = "another@test.com",
      Username = "EXISTING_NAME",
      Password = "Aa@12345"
    });

    output.Success.Should().BeFalse();
    output.Message.Should().Be("Username đã tồn tại.");
  }

  [Fact]
  public async Task RegisterAsync_TC04_BusinessRule_MissingReaderRoleStillCreatesUser()
  {
    await using var db = CreateInMemoryDbContext();
    var service = CreateService(db);

    var output = await service.RegisterAsync(new RegisterDto
    {
      Email = "norole@test.com",
      Username = "norole_user",
      Password = "Aa@12345"
    });

    output.Success.Should().BeTrue();

    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == "norole@test.com");
    user.Should().NotBeNull();
    (await db.UserRoles.AnyAsync()).Should().BeFalse();
  }

  [Fact]
  public async Task RegisterAsync_TC05_Exception_WhenOtpGenerationFails()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    var service = CreateService(db);

    _mockOtpService
      .Setup(x => x.GenerateOtpAsync("otpfail@test.com"))
      .ThrowsAsync(new InvalidOperationException("otp-error"));

    var act = () => service.RegisterAsync(new RegisterDto
    {
      Email = "otpfail@test.com",
      Username = "otp_fail_user",
      Password = "Aa@12345"
    });

    await act.Should().ThrowAsync<InvalidOperationException>();
    (await db.Users.AnyAsync(u => u.Email == "otpfail@test.com")).Should().BeTrue();
  }

  [Fact]
  public async Task VerifyEmailOtpAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 20,
      Email = "verify@test.com",
      Username = "verify_user",
      DisplayName = "Verify User",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = false,
      IsActive = true
    });
    await db.SaveChangesAsync();

    _mockOtpService
      .Setup(x => x.ValidateOtp("verify@test.com", "654321"))
      .Returns(true);

    var service = CreateService(db);

    var output = await service.VerifyEmailOtpAsync(new VerifyOtpDto
    {
      Email = "verify@test.com",
      Code = "654321"
    });

    _output.WriteLine("Input: Email=verify@test.com, Code=654321");
    _output.WriteLine($"Output: Success={output.Success}, Message={output.Message}");

    output.Success.Should().BeTrue();
    output.Message.Should().Be("Xác thực email thành công. Bạn có thể đăng nhập.");

    var user = await db.Users.FirstAsync(u => u.UserId == 20);
    user.IsEmailVerified.Should().BeTrue();
  }

  [Fact]
  public async Task VerifyEmailOtpAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);
    var service = CreateService(db);

    _mockOtpService
      .Setup(x => x.ValidateOtp("verify@test.com", "badotp"))
      .Returns(false);

    var output = await service.VerifyEmailOtpAsync(new VerifyOtpDto
    {
      Email = "verify@test.com",
      Code = "badotp"
    });

    _output.WriteLine("Input: invalid OTP");
    _output.WriteLine($"Output: Success={output.Success}, Message={output.Message}");

    output.Success.Should().BeFalse();
    output.Message.Should().Be("Mã OTP không hợp lệ hoặc đã hết hạn.");
  }

  [Fact]
  public async Task VerifyEmailOtpAsync_TC03_NotFound_WhenOtpValidButUserMissing()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    _mockOtpService
      .Setup(x => x.ValidateOtp("nouser@test.com", "123123"))
      .Returns(true);

    var service = CreateService(db);
    var output = await service.VerifyEmailOtpAsync(new VerifyOtpDto
    {
      Email = "nouser@test.com",
      Code = "123123"
    });

    output.Success.Should().BeFalse();
    output.Message.Should().Be("Tài khoản không tồn tại.");
  }

  [Fact]
  public async Task VerifyEmailOtpAsync_TC04_BusinessRule_EmailLookupIsCaseInsensitive()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 21,
      Email = "caseverify@test.com",
      Username = "case_verify",
      DisplayName = "Case Verify",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = false,
      IsActive = true
    });
    await db.SaveChangesAsync();

    _mockOtpService
      .Setup(x => x.ValidateOtp("CASEVERIFY@TEST.COM", "111222"))
      .Returns(true);

    var service = CreateService(db);
    var output = await service.VerifyEmailOtpAsync(new VerifyOtpDto
    {
      Email = "CASEVERIFY@TEST.COM",
      Code = "111222"
    });

    output.Success.Should().BeTrue();
    (await db.Users.FirstAsync(u => u.UserId == 21)).IsEmailVerified.Should().BeTrue();
  }

  [Fact]
  public async Task VerifyEmailOtpAsync_TC05_Exception_WhenOtpValidatorThrows()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    var service = CreateService(db);

    _mockOtpService
      .Setup(x => x.ValidateOtp("throwverify@test.com", "x"))
      .Throws(new InvalidOperationException("otp-validation-error"));
    var act = () => service.VerifyEmailOtpAsync(new VerifyOtpDto
    {
      Email = "throwverify@test.com",
      Code = "x"
    });

    await act.Should().ThrowAsync<InvalidOperationException>();
  }

  [Fact]
  public async Task LoginAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    var user = new User
    {
      UserId = 30,
      Email = "login@test.com",
      Username = "login_user",
      DisplayName = "Login User",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = true,
      IsActive = true
    };
    db.Users.Add(user);
    await db.SaveChangesAsync();

    db.UserRoles.Add(new UserRole
    {
      UserId = user.UserId,
      RoleId = 1,
      AssignedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.LoginAsync(new LoginDto { Username = "login_user", Password = "Aa@12345" });

    _output.WriteLine("Input: username=login_user, password=correct");
    _output.WriteLine($"Output: AccessToken={output?.AccessToken}, RefreshToken={output?.RefreshToken}, UserId={output?.UserId}");

    output.Should().NotBeNull();
    output!.AccessToken.Should().Be("access-token");
    output.RefreshToken.Should().Be("refresh-token");
    output.UserId.Should().Be(30);
    output.Roles.Should().Contain("READER");
  }

  [Fact]
  public async Task LoginAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 31,
      Email = "inactive@test.com",
      Username = "inactive_user",
      DisplayName = "Inactive User",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = false,
      IsActive = true
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.LoginAsync(new LoginDto { Username = "inactive_user", Password = "Aa@12345" });

    _output.WriteLine("Input: username=inactive_user, email not verified");
    _output.WriteLine($"Output: {(output is null ? "null" : "non-null")}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task LoginAsync_TC03_BusinessRule_AdminCredentialCreatesAdminRole()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    var service = CreateService(db);
    var output = await service.LoginAsync(new LoginDto
    {
      Username = "admin",
      Password = "Admin@123"
    });

    output.Should().NotBeNull();
    output!.Roles.Should().Contain("ADMIN");

    var admin = await db.Users.FirstAsync(u => u.Username == "admin");
    admin.IsEmailVerified.Should().BeTrue();
    (await db.UserRoles.AnyAsync(ur => ur.UserId == admin.UserId && ur.RoleId == 2)).Should().BeTrue();
  }

  [Fact]
  public async Task LoginAsync_TC04_InvalidInput_WrongPassword()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 32,
      Email = "wrongpwd@test.com",
      Username = "wrong_pwd_user",
      DisplayName = "Wrong Password User",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = true,
      IsActive = true
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.LoginAsync(new LoginDto { Username = "wrong_pwd_user", Password = "bad-password" });

    output.Should().BeNull();
  }

  [Fact]
  public async Task LoginAsync_TC05_InvalidInput_InactiveUser()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 33,
      Email = "inactive2@test.com",
      Username = "inactive_user_2",
      DisplayName = "Inactive User 2",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = true,
      IsActive = false
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.LoginAsync(new LoginDto { Username = "inactive_user_2", Password = "Aa@12345" });

    output.Should().BeNull();
  }

  [Fact]
  public async Task RefreshAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    var user = new User
    {
      UserId = 40,
      Email = "refresh@test.com",
      Username = "refresh_user",
      DisplayName = "Refresh User",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = true,
      IsActive = true,
      RefreshToken = "old-refresh-token",
      RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(2)
    };
    db.Users.Add(user);
    await db.SaveChangesAsync();

    db.UserRoles.Add(new UserRole
    {
      UserId = user.UserId,
      RoleId = 1,
      AssignedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    _mockTokenService.Setup(x => x.GenerateRefreshToken()).Returns("new-refresh-token");
    var output = await service.RefreshAsync(new TokenApiDto
    {
      AccessToken = "old-access-token",
      RefreshToken = "old-refresh-token"
    });

    _output.WriteLine("Input: valid refresh token");
    _output.WriteLine($"Output: AccessToken={output?.AccessToken}, RefreshToken={output?.RefreshToken}");

    output.Should().NotBeNull();
    output!.AccessToken.Should().Be("access-token");
    output.RefreshToken.Should().Be("new-refresh-token");

    var updated = await db.Users.FirstAsync(u => u.UserId == 40);
    updated.RefreshToken.Should().Be("new-refresh-token");
  }

  [Fact]
  public async Task RefreshAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);
    var service = CreateService(db);

    var output = await service.RefreshAsync(new TokenApiDto
    {
      AccessToken = "x",
      RefreshToken = ""
    });

    _output.WriteLine("Input: empty refresh token");
    _output.WriteLine($"Output: {(output is null ? "null" : "non-null")}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task RefreshAsync_TC03_NotFound_WhenRefreshTokenDoesNotExist()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);
    var service = CreateService(db);

    var output = await service.RefreshAsync(new TokenApiDto
    {
      AccessToken = "old-access-token",
      RefreshToken = "missing-token"
    });

    output.Should().BeNull();
  }

  [Fact]
  public async Task RefreshAsync_TC04_InvalidInput_WhenRefreshTokenExpired()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 41,
      Email = "expired-refresh@test.com",
      Username = "expired_refresh_user",
      DisplayName = "Expired Refresh User",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = true,
      IsActive = true,
      RefreshToken = "expired-refresh-token",
      RefreshTokenExpiryTime = DateTime.UtcNow.AddMinutes(-1)
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.RefreshAsync(new TokenApiDto
    {
      AccessToken = "old-access-token",
      RefreshToken = "expired-refresh-token"
    });

    output.Should().BeNull();
  }

  [Fact]
  public async Task RefreshAsync_TC05_BusinessRule_DoesNotRequireAccessTokenValue()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 42,
      Email = "refresh-no-access@test.com",
      Username = "refresh_no_access",
      DisplayName = "Refresh No Access",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = true,
      IsActive = true,
      RefreshToken = "refresh-no-access-token",
      RefreshTokenExpiryTime = DateTime.UtcNow.AddHours(2)
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    _mockTokenService.Setup(x => x.GenerateRefreshToken()).Returns("rotated-refresh-token");
    var output = await service.RefreshAsync(new TokenApiDto
    {
      AccessToken = string.Empty,
      RefreshToken = "refresh-no-access-token"
    });

    output.Should().NotBeNull();
    output!.RefreshToken.Should().Be("rotated-refresh-token");
  }

  [Fact]
  public async Task LogoutAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);
    var service = CreateService(db);

    var token = BuildJwtTokenString(DateTime.UtcNow.AddMinutes(30));
    var output = await service.LogoutAsync(token);

    _output.WriteLine($"Input: jwt={token.Substring(0, Math.Min(20, token.Length))}...");
    _output.WriteLine($"Output: Success={output.Success}, Message={output.Message}");

    output.Success.Should().BeTrue();
    output.Message.Should().Be("Đăng xuất thành công.");

    _mockTokenService.Verify(x => x.BlacklistToken(token, It.IsAny<DateTime>()), Times.Once);
  }

  [Fact]
  public async Task LogoutAsync_TC02_InvalidInput_EmptyTokenThrows()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);
    var service = CreateService(db);

    var act = () => service.LogoutAsync(string.Empty);

    await act.Should().ThrowAsync<ArgumentException>();
  }

  [Fact]
  public async Task LogoutAsync_TC03_InvalidInput_MalformedTokenThrows()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);
    var service = CreateService(db);

    var act = () => service.LogoutAsync("not-a-jwt-token");

    await act.Should().ThrowAsync<ArgumentException>();
  }

  [Fact]
  public async Task ForgotPasswordAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 50,
      Email = "forgot@test.com",
      Username = "forgot_user",
      DisplayName = "Forgot User",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = true,
      IsActive = true
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.ForgotPasswordAsync("forgot@test.com");

    _output.WriteLine("Input: email=forgot@test.com");
    _output.WriteLine($"Output: Success={output.Success}, Message={output.Message}");

    output.Success.Should().BeTrue();
    output.Message.Should().Be("Mã xác minh đã được gửi đến email của bạn.");

    _mockOtpService.Verify(x => x.GenerateOtpAsync("forgot@test.com"), Times.Once);
    _mockEmailService.Verify(x => x.SendOtpEmailAsync("forgot@test.com", "123456"), Times.Once);
  }

  [Fact]
  public async Task ForgotPasswordAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);
    var service = CreateService(db);

    var output = await service.ForgotPasswordAsync("missing@test.com");

    _output.WriteLine("Input: email=missing@test.com (not found)");
    _output.WriteLine($"Output: Success={output.Success}, Message={output.Message}");

    output.Success.Should().BeTrue();
    output.Message.Should().Be("Nếu email tồn tại, chúng tôi đã gửi mã xác minh.");

    _mockOtpService.Verify(x => x.GenerateOtpAsync(It.IsAny<string>()), Times.Never);
    _mockEmailService.Verify(x => x.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
  }

  [Fact]
  public async Task ForgotPasswordAsync_TC03_BusinessRule_UnverifiedOrInactiveReturnsGenericMessage()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 51,
      Email = "forgot-inactive@test.com",
      Username = "forgot_inactive",
      DisplayName = "Forgot Inactive",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = false,
      IsActive = false
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.ForgotPasswordAsync("forgot-inactive@test.com");

    output.Success.Should().BeTrue();
    output.Message.Should().Be("Nếu email tồn tại, chúng tôi đã gửi mã xác minh.");
    _mockOtpService.Verify(x => x.GenerateOtpAsync(It.IsAny<string>()), Times.Never);
    _mockEmailService.Verify(x => x.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
  }

  [Fact]
  public async Task ResetPasswordAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 60,
      Email = "reset@test.com",
      Username = "reset_user",
      DisplayName = "Reset User",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = true,
      IsActive = true,
      RefreshToken = "legacy-token",
      RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1)
    });
    await db.SaveChangesAsync();

    _mockOtpService
      .Setup(x => x.ValidateOtp("reset@test.com", "999999"))
      .Returns(true);

    var service = CreateService(db);
    var output = await service.ResetPasswordAsync("reset@test.com", "999999", "Bb@12345");

    _output.WriteLine("Input: email=reset@test.com, otp=999999");
    _output.WriteLine($"Output: Success={output.Success}, Message={output.Message}");

    output.Success.Should().BeTrue();
    output.Message.Should().Be("Đặt lại mật khẩu thành công. Vui lòng đăng nhập.");

    var updated = await db.Users.FirstAsync(u => u.UserId == 60);
    BCrypt.Net.BCrypt.Verify("Bb@12345", updated.PasswordHash).Should().BeTrue();
    updated.RefreshToken.Should().BeNull();
    updated.RefreshTokenExpiryTime.Should().BeNull();
  }

  [Fact]
  public async Task ResetPasswordAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);
    var service = CreateService(db);

    _mockOtpService
      .Setup(x => x.ValidateOtp("reset@test.com", "bad"))
      .Returns(false);

    var output = await service.ResetPasswordAsync("reset@test.com", "bad", "Bb@12345");

    _output.WriteLine("Input: invalid otp for reset");
    _output.WriteLine($"Output: Success={output.Success}, Message={output.Message}");

    output.Success.Should().BeFalse();
    output.Message.Should().Be("Mã OTP không hợp lệ hoặc đã hết hạn.");
  }

  [Fact]
  public async Task ResetPasswordAsync_TC03_NotFound_WhenOtpValidButUserMissing()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    _mockOtpService
      .Setup(x => x.ValidateOtp("nouser-reset@test.com", "123123"))
      .Returns(true);

    var service = CreateService(db);
    var output = await service.ResetPasswordAsync("nouser-reset@test.com", "123123", "Bb@12345");

    output.Success.Should().BeFalse();
    output.Message.Should().Be("Tài khoản không tồn tại.");
  }

  [Fact]
  public async Task ResetPasswordAsync_TC04_BusinessRule_EmailLookupIsCaseInsensitive()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 61,
      Email = "casereset@test.com",
      Username = "case_reset",
      DisplayName = "Case Reset",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = true,
      IsActive = true
    });
    await db.SaveChangesAsync();

    _mockOtpService
      .Setup(x => x.ValidateOtp("CASERESET@TEST.COM", "456789"))
      .Returns(true);

    var service = CreateService(db);
    var output = await service.ResetPasswordAsync("CASERESET@TEST.COM", "456789", "Bb@12345");

    output.Success.Should().BeTrue();
    output.Message.Should().Be("Đặt lại mật khẩu thành công. Vui lòng đăng nhập.");
  }

  [Fact]
  public async Task ResetPasswordAsync_TC05_Exception_WhenOtpValidatorThrows()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    var service = CreateService(db);

    _mockOtpService
      .Setup(x => x.ValidateOtp("throw-reset@test.com", "x"))
      .Throws(new InvalidOperationException("otp-validate-error"));

    var act = () => service.ResetPasswordAsync("throw-reset@test.com", "x", "Bb@12345");

    await act.Should().ThrowAsync<InvalidOperationException>();
  }

  [Fact]
  public async Task ChangePasswordAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 70,
      Email = "change@test.com",
      Username = "change_user",
      DisplayName = "Change User",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = true,
      IsActive = true,
      RefreshToken = "r1",
      RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1)
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.ChangePasswordAsync(70, "Aa@12345", "Cc@12345");

    _output.WriteLine("Input: userId=70, current=correct, new=different");
    _output.WriteLine($"Output: Success={output.Success}, Message={output.Message}");

    output.Success.Should().BeTrue();
    output.Message.Should().Be("Đổi mật khẩu thành công.");

    var updated = await db.Users.FirstAsync(u => u.UserId == 70);
    BCrypt.Net.BCrypt.Verify("Cc@12345", updated.PasswordHash).Should().BeTrue();
    updated.RefreshToken.Should().BeNull();
    updated.RefreshTokenExpiryTime.Should().BeNull();
  }

  [Fact]
  public async Task ChangePasswordAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 71,
      Email = "change2@test.com",
      Username = "change_user2",
      DisplayName = "Change User 2",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = true,
      IsActive = true
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.ChangePasswordAsync(71, "wrong-current", "Cc@12345");

    _output.WriteLine("Input: wrong current password");
    _output.WriteLine($"Output: Success={output.Success}, Message={output.Message}");

    output.Success.Should().BeFalse();
    output.Message.Should().Be("Mật khẩu hiện tại không đúng.");
  }

  [Fact]
  public async Task ChangePasswordAsync_TC03_NotFound_WhenUserMissing()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);
    var service = CreateService(db);

    var output = await service.ChangePasswordAsync(9999, "Aa@12345", "Cc@12345");

    output.Success.Should().BeFalse();
    output.Message.Should().Be("Tài khoản không tồn tại.");
  }

  [Fact]
  public async Task ChangePasswordAsync_TC04_BusinessRule_RejectSamePassword()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 72,
      Email = "samepwd@test.com",
      Username = "same_pwd_user",
      DisplayName = "Same Password User",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = true,
      IsActive = true
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.ChangePasswordAsync(72, "Aa@12345", "Aa@12345");

    output.Success.Should().BeFalse();
    output.Message.Should().Be("Mật khẩu mới không được trùng với mật khẩu hiện tại.");
  }

  [Fact]
  public async Task ChangePasswordAsync_TC05_InvalidInput_WhenPasswordHashMissing()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 73,
      Email = "nohash@test.com",
      Username = "no_hash_user",
      DisplayName = "No Hash User",
      PasswordHash = string.Empty,
      IsEmailVerified = true,
      IsActive = true
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.ChangePasswordAsync(73, "Aa@12345", "Cc@12345");

    output.Success.Should().BeFalse();
    output.Message.Should().Be("Mật khẩu hiện tại không đúng.");
  }

  [Fact]
  public async Task GoogleLoginAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    _mockGoogleOAuth
      .Setup(x => x.VerifyTokenAsync("google-token"))
      .ReturnsAsync(new SocialUserInfoDto
      {
        SocialId = "g-100",
        Email = "social@test.com",
        Name = "Social User"
      });

    var service = CreateService(db);
    var output = await service.GoogleLoginAsync(new GoogleLoginDto { IdToken = "google-token" });

    _output.WriteLine("Input: valid Google id token");
    _output.WriteLine($"Output: AccessToken={output?.AccessToken}, Username={output?.Username}, Email={output?.Email}");

    output.Should().NotBeNull();
    output!.AccessToken.Should().Be("access-token");
    output.Email.Should().Be("social@test.com");

    var created = await db.Users.FirstAsync(u => u.Email == "social@test.com");
    created.GoogleId.Should().Be("g-100");
    created.IsEmailVerified.Should().BeTrue();
  }

  [Fact]
  public async Task GoogleLoginAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    _mockGoogleOAuth
      .Setup(x => x.VerifyTokenAsync("bad-token"))
      .ReturnsAsync((SocialUserInfoDto?)null);

    var service = CreateService(db);
    var output = await service.GoogleLoginAsync(new GoogleLoginDto { IdToken = "bad-token" });

    _output.WriteLine("Input: invalid Google id token");
    _output.WriteLine($"Output: {(output is null ? "null" : "non-null")}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task GoogleLoginAsync_TC03_BusinessRule_MergeExistingAccount()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 80,
      Email = "merge-google@test.com",
      Username = "merge_google",
      DisplayName = "Merge Google",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = false,
      IsActive = true
    });
    await db.SaveChangesAsync();

    _mockGoogleOAuth
      .Setup(x => x.VerifyTokenAsync("merge-google-token"))
      .ReturnsAsync(new SocialUserInfoDto
      {
        SocialId = "g-merge",
        Email = "merge-google@test.com",
        Name = "Merge Google Name"
      });

    var service = CreateService(db);
    var output = await service.GoogleLoginAsync(new GoogleLoginDto { IdToken = "merge-google-token" });

    output.Should().NotBeNull();
    var merged = await db.Users.FirstAsync(u => u.UserId == 80);
    merged.GoogleId.Should().Be("g-merge");
    merged.IsEmailVerified.Should().BeTrue();
  }

  [Fact]
  public async Task GoogleLoginAsync_TC04_BusinessRule_DoesNotOverrideExistingGoogleId()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 81,
      Email = "keep-google@test.com",
      Username = "keep_google",
      DisplayName = "Keep Google",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      GoogleId = "g-original",
      IsEmailVerified = true,
      IsActive = true
    });
    await db.SaveChangesAsync();

    _mockGoogleOAuth
      .Setup(x => x.VerifyTokenAsync("keep-google-token"))
      .ReturnsAsync(new SocialUserInfoDto
      {
        SocialId = "g-new",
        Email = "keep-google@test.com",
        Name = "Keep Google Name"
      });

    var service = CreateService(db);
    var output = await service.GoogleLoginAsync(new GoogleLoginDto { IdToken = "keep-google-token" });

    output.Should().NotBeNull();
    (await db.Users.FirstAsync(u => u.UserId == 81)).GoogleId.Should().Be("g-original");
  }

  [Fact]
  public async Task GoogleLoginAsync_TC05_Exception_WhenGoogleVerifyThrows()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    _mockGoogleOAuth
      .Setup(x => x.VerifyTokenAsync("throw-google-token"))
      .ThrowsAsync(new InvalidOperationException("google-verify-error"));

    var service = CreateService(db);
    var act = () => service.GoogleLoginAsync(new GoogleLoginDto { IdToken = "throw-google-token" });

    await act.Should().ThrowAsync<InvalidOperationException>();
  }

  [Fact]
  public async Task FacebookLoginAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    _mockFacebookOAuth
      .Setup(x => x.VerifyTokenAsync("fb-token"))
      .ReturnsAsync(new SocialUserInfoDto
      {
        SocialId = "fb-200",
        Email = "fbsocial@test.com",
        Name = "Facebook User"
      });

    var service = CreateService(db);
    var output = await service.FacebookLoginAsync(new FacebookLoginDto { AccessToken = "fb-token" });

    _output.WriteLine("Input: valid Facebook access token");
    _output.WriteLine($"Output: AccessToken={output?.AccessToken}, Username={output?.Username}, Email={output?.Email}");

    output.Should().NotBeNull();
    output!.AccessToken.Should().Be("access-token");
    output.Email.Should().Be("fbsocial@test.com");

    var created = await db.Users.FirstAsync(u => u.Email == "fbsocial@test.com");
    created.FacebookId.Should().Be("fb-200");
    created.IsEmailVerified.Should().BeTrue();
  }

  [Fact]
  public async Task FacebookLoginAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    _mockFacebookOAuth
      .Setup(x => x.VerifyTokenAsync("bad-fb-token"))
      .ReturnsAsync((SocialUserInfoDto?)null);

    var service = CreateService(db);
    var output = await service.FacebookLoginAsync(new FacebookLoginDto { AccessToken = "bad-fb-token" });

    _output.WriteLine("Input: invalid Facebook token");
    _output.WriteLine($"Output: {(output is null ? "null" : "non-null")}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task FacebookLoginAsync_TC03_BusinessRule_MergeExistingAccount()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 82,
      Email = "merge-facebook@test.com",
      Username = "merge_facebook",
      DisplayName = "Merge Facebook",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      IsEmailVerified = false,
      IsActive = true
    });
    await db.SaveChangesAsync();

    _mockFacebookOAuth
      .Setup(x => x.VerifyTokenAsync("merge-fb-token"))
      .ReturnsAsync(new SocialUserInfoDto
      {
        SocialId = "fb-merge",
        Email = "merge-facebook@test.com",
        Name = "Merge Facebook Name"
      });

    var service = CreateService(db);
    var output = await service.FacebookLoginAsync(new FacebookLoginDto { AccessToken = "merge-fb-token" });

    output.Should().NotBeNull();
    var merged = await db.Users.FirstAsync(u => u.UserId == 82);
    merged.FacebookId.Should().Be("fb-merge");
    merged.IsEmailVerified.Should().BeTrue();
  }

  [Fact]
  public async Task FacebookLoginAsync_TC04_BusinessRule_DoesNotOverrideExistingFacebookId()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    db.Users.Add(new User
    {
      UserId = 83,
      Email = "keep-facebook@test.com",
      Username = "keep_facebook",
      DisplayName = "Keep Facebook",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa@12345"),
      FacebookId = "fb-original",
      IsEmailVerified = true,
      IsActive = true
    });
    await db.SaveChangesAsync();

    _mockFacebookOAuth
      .Setup(x => x.VerifyTokenAsync("keep-fb-token"))
      .ReturnsAsync(new SocialUserInfoDto
      {
        SocialId = "fb-new",
        Email = "keep-facebook@test.com",
        Name = "Keep Facebook Name"
      });

    var service = CreateService(db);
    var output = await service.FacebookLoginAsync(new FacebookLoginDto { AccessToken = "keep-fb-token" });

    output.Should().NotBeNull();
    (await db.Users.FirstAsync(u => u.UserId == 83)).FacebookId.Should().Be("fb-original");
  }

  [Fact]
  public async Task FacebookLoginAsync_TC05_Exception_WhenFacebookVerifyThrows()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRolesAsync(db);

    _mockFacebookOAuth
      .Setup(x => x.VerifyTokenAsync("throw-fb-token"))
      .ThrowsAsync(new InvalidOperationException("facebook-verify-error"));

    var service = CreateService(db);
    var act = () => service.FacebookLoginAsync(new FacebookLoginDto { AccessToken = "throw-fb-token" });

    await act.Should().ThrowAsync<InvalidOperationException>();
  }

  private static string BuildJwtTokenString(DateTime utcExpiry)
  {
    var token = new JwtSecurityToken(
      issuer: "mlndex-tests",
      audience: "mlndex-tests",
      claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, "logout-user") },
      notBefore: DateTime.UtcNow.AddMinutes(-1),
      expires: utcExpiry);

    return new JwtSecurityTokenHandler().WriteToken(token);
  }
}
