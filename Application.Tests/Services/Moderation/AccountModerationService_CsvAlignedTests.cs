using System;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.Moderation;
using Application.Services.Moderation;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.Moderation;

public class AccountModerationService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public AccountModerationService_CsvAlignedTests(ITestOutputHelper output)
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

  private static async Task SeedBaseAsync(MlndexDbContext db)
  {
    db.Roles.AddRange(
      new Role { RoleId = 1, RoleName = RoleName.READER },
      new Role { RoleId = 2, RoleName = RoleName.ADMIN },
      new Role { RoleId = 3, RoleName = RoleName.MODERATOR });

    db.Users.AddRange(
      new User { UserId = 1, Username = "user1", Email = "u1@test.com", DisplayName = "User1", PasswordHash = "hash", IsActive = true },
      new User { UserId = 2, Username = "user2", Email = "u2@test.com", DisplayName = "User2", PasswordHash = "hash", IsActive = false });

    db.UserRoles.AddRange(
      new UserRole { UserRoleId = 1, UserId = 1, RoleId = 1, AssignedAt = DateTime.UtcNow.AddDays(-2) },
      new UserRole { UserRoleId = 2, UserId = 1, RoleId = 2, AssignedAt = DateTime.UtcNow.AddDays(-1) });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task ApplyAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    var service = new AccountModerationService(db);

    var output = await service.ApplyAsync(1, 99, new AccountActionRequest
    {
      Action = AccountActionType.DEACTIVATE,
      Reason = "Violation"
    });

    _output.WriteLine("Input: userId=1 action=DEACTIVATE");
    _output.WriteLine($"Output: IsActive={output.IsActive}, Message={output.Message}");

    output.IsActive.Should().BeFalse();
    output.Message.Should().Be("Đã vô hiệu hóa tài khoản");
  }

  [Fact]
  public async Task ApplyAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    var service = new AccountModerationService(db);

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.ApplyAsync(1, 99, new AccountActionRequest
    {
      Action = (AccountActionType)999,
      Reason = "Invalid action"
    }));

    _output.WriteLine("Input: invalid action enum");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task ApplyAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new AccountModerationService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.ApplyAsync(1, 99, new AccountActionRequest
    {
      Action = AccountActionType.WARN,
      Reason = "x"
    }));

    _output.WriteLine("Input: disposed context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task UpdateRolesAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    var service = new AccountModerationService(db);

    var output = await service.UpdateRolesAsync(1, new UpdateUserRolesRequest
    {
      Roles = { "MODERATOR", "READER" }
    });

    _output.WriteLine("Input: userId=1 roles=[MODERATOR,READER]");
    _output.WriteLine($"Output: Updated={output}");

    output.Should().BeTrue();
    var roleIds = await db.UserRoles.Where(ur => ur.UserId == 1).Select(ur => ur.RoleId).OrderBy(x => x).ToListAsync();
    roleIds.Should().Equal(new[] { 1, 3 });
  }

  [Fact]
  public async Task UpdateRolesAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    var service = new AccountModerationService(db);

    var output = await service.UpdateRolesAsync(1, new UpdateUserRolesRequest
    {
      Roles = { "UNKNOWN_ROLE" }
    });

    _output.WriteLine("Input: invalid role string");
    _output.WriteLine($"Output: Updated={output}, RoleCount={await db.UserRoles.CountAsync(ur => ur.UserId == 1)}");

    output.Should().BeTrue();
    (await db.UserRoles.CountAsync(ur => ur.UserId == 1)).Should().Be(0);
  }

  [Fact]
  public async Task UpdateRolesAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new AccountModerationService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.UpdateRolesAsync(1, new UpdateUserRolesRequest
    {
      Roles = { "READER" }
    }));

    _output.WriteLine("Input: disposed context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }
}
