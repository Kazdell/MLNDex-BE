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

public class ModeratorAdminService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public ModeratorAdminService_CsvAlignedTests(ITestOutputHelper output)
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
      new Role { RoleId = 2, RoleName = RoleName.MODERATOR });

    db.Users.AddRange(
      new User { UserId = 1, Username = "mod1", Email = "mod1@test.com", DisplayName = "Mod One", PasswordHash = "hash", IsActive = true },
      new User { UserId = 2, Username = "mod2", Email = "mod2@test.com", DisplayName = "Mod Two", PasswordHash = "hash", IsActive = false },
      new User { UserId = 3, Username = "reader", Email = "reader@test.com", DisplayName = "Reader", PasswordHash = "hash", IsActive = true });

    db.UserRoles.AddRange(
      new UserRole { UserRoleId = 1, UserId = 1, RoleId = 2, AssignedAt = DateTime.UtcNow.AddDays(-2) },
      new UserRole { UserRoleId = 2, UserId = 2, RoleId = 2, AssignedAt = DateTime.UtcNow.AddDays(-1) },
      new UserRole { UserRoleId = 3, UserId = 3, RoleId = 1, AssignedAt = DateTime.UtcNow.AddDays(-3) });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task GetModeratorsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    var service = new ModeratorAdminService(db);

    var output = await service.GetModeratorsAsync(new ModeratorListRequest { Page = 1, PageSize = 20 });

    _output.WriteLine("Input: Page=1, PageSize=20");
    _output.WriteLine($"Output: Total={output.TotalCount}, FirstUser={output.Items.FirstOrDefault()?.Username}");

    output.TotalCount.Should().Be(2);
    output.Items.Should().HaveCount(2);
  }

  [Fact]
  public async Task GetModeratorsAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    var service = new ModeratorAdminService(db);

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetModeratorsAsync(null!));

    _output.WriteLine("Input: request=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetModeratorsAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new ModeratorAdminService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetModeratorsAsync(new ModeratorListRequest()));

    _output.WriteLine("Input: disposed context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task AssignAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    var service = new ModeratorAdminService(db);

    var output = await service.AssignAsync(3);

    _output.WriteLine("Input: userId=3 assign moderator");
    _output.WriteLine($"Output: User={output.Username}, AssignedAt={output.AssignedAt}");

    output.UserId.Should().Be(3);
    (await db.UserRoles.CountAsync(ur => ur.UserId == 3 && ur.RoleId == 2)).Should().Be(1);
  }

  [Fact]
  public async Task AssignAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    var service = new ModeratorAdminService(db);

    var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AssignAsync(0));

    _output.WriteLine("Input: userId=0");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("không tồn tại");
  }

  [Fact]
  public async Task AssignAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new ModeratorAdminService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.AssignAsync(1));

    _output.WriteLine("Input: disposed context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task RemoveAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    var service = new ModeratorAdminService(db);

    await service.RemoveAsync(1);

    _output.WriteLine("Input: remove moderator userId=1");
    _output.WriteLine($"Output: RemainingRoles={await db.UserRoles.CountAsync(ur => ur.UserId == 1 && ur.RoleId == 2)}");

    (await db.UserRoles.AnyAsync(ur => ur.UserId == 1 && ur.RoleId == 2)).Should().BeFalse();
  }

  [Fact]
  public async Task RemoveAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    var service = new ModeratorAdminService(db);

    var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RemoveAsync(0));

    _output.WriteLine("Input: userId=0 remove");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("không tồn tại");
  }

  [Fact]
  public async Task RemoveAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new ModeratorAdminService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.RemoveAsync(1));

    _output.WriteLine("Input: disposed context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }
}
