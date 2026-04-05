using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.Moderation;
using Application.Services.Moderation;
using Application.Tests.Shared;
using Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.Tests.Services.Moderation
{
  [Collection("Database collection")]
  public class AccountModerationServiceTests : IAsyncLifetime
  {
    private readonly DatabaseFixture _fixture;
    private Infrastructure.Persistence.Data.MlndexDbContext _context = default!;
    private AccountModerationService _service = default!;

    public AccountModerationServiceTests(DatabaseFixture fixture)
    {
      _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
      await _fixture.ResetDatabaseAsync();
      _context = _fixture.CreateDbContext();
      _service = new AccountModerationService(_context);
      await SeedRolesAsync();
    }

    public async Task DisposeAsync()
    {
      if (_context != null)
        await _context.DisposeAsync();
    }

    private async Task SeedRolesAsync()
    {
      _context.Roles.AddRange(
          new Role { RoleId = 1, RoleName = RoleName.READER },
          new Role { RoleId = 2, RoleName = RoleName.CREATOR },
          new Role { RoleId = 3, RoleName = RoleName.TRANSLATOR },
          new Role { RoleId = 4, RoleName = RoleName.MODERATOR },
          new Role { RoleId = 5, RoleName = RoleName.ADMIN }
      );
      await _context.SaveChangesAsync();
    }

    private async Task<User> CreateUserWithRolesAsync(int userId, string username, params int[] roleIds)
    {
      var user = new User
      {
        UserId = userId,
        Username = username,
        DisplayName = username,
        Email = $"{username.ToLower()}@test.com",
        PasswordHash = "hash_placeholder",
        IsActive = true
      };
      _context.Users.Add(user);
      await _context.SaveChangesAsync();

      foreach (var roleId in roleIds)
      {
        _context.UserRoles.Add(new UserRole
        {
          UserId = userId,
          RoleId = roleId,
          AssignedAt = DateTime.UtcNow
        });
      }
      await _context.SaveChangesAsync();

      return user;
    }

    // ============================================================
    // TESTS: ApplyAsync — Warn / Deactivate / Activate actions
    // ============================================================

    [Fact]
    public async Task ApplyAsync_Moderator_Cannot_Ban_Admin()
    {
      var adminUser = await CreateUserWithRolesAsync(1, "admin1", 5);
      var modUser = await CreateUserWithRolesAsync(2, "mod1", 4);

      var request = new AccountActionRequest { Action = AccountActionType.DEACTIVATE, Reason = "Test" };

      var act = async () => await _service.ApplyAsync(adminUser.UserId, modUser.UserId, request);
      await act.Should().ThrowAsync<UnauthorizedAccessException>()
          .WithMessage("*Hệ thống Quản trị*");
    }

    [Fact]
    public async Task ApplyAsync_Moderator_Cannot_Warn_Another_Moderator()
    {
      var modTarget = await CreateUserWithRolesAsync(1, "mod_target", 4);
      var modActor = await CreateUserWithRolesAsync(2, "mod_actor", 4);

      var request = new AccountActionRequest { Action = AccountActionType.WARN, Reason = "Test warn" };

      var act = async () => await _service.ApplyAsync(modTarget.UserId, modActor.UserId, request);
      await act.Should().ThrowAsync<UnauthorizedAccessException>()
          .WithMessage("*Hệ thống Quản trị*");
    }

    [Fact]
    public async Task ApplyAsync_Admin_Cannot_Ban_Another_Admin()
    {
      var admin1 = await CreateUserWithRolesAsync(1, "admin1", 5);
      var admin2 = await CreateUserWithRolesAsync(2, "admin2", 5);

      var request = new AccountActionRequest { Action = AccountActionType.DEACTIVATE, Reason = "Power struggle" };

      var act = async () => await _service.ApplyAsync(admin1.UserId, admin2.UserId, request);
      await act.Should().ThrowAsync<UnauthorizedAccessException>()
          .WithMessage("*Admin không thể*");
    }

    [Fact]
    public async Task ApplyAsync_Admin_Can_Ban_Moderator()
    {
      var modUser = await CreateUserWithRolesAsync(1, "mod1", 4);
      var adminUser = await CreateUserWithRolesAsync(2, "admin1", 5);

      var request = new AccountActionRequest { Action = AccountActionType.DEACTIVATE, Reason = "Violation" };

      var result = await _service.ApplyAsync(modUser.UserId, adminUser.UserId, request);

      result.IsActive.Should().BeFalse();
      result.Message.Should().Contain("vô hiệu hóa");
    }

    [Fact]
    public async Task ApplyAsync_Admin_Can_Warn_Regular_User()
    {
      var readerUser = await CreateUserWithRolesAsync(1, "reader1", 1);
      var adminUser = await CreateUserWithRolesAsync(2, "admin1", 5);

      var request = new AccountActionRequest { Action = AccountActionType.WARN, Reason = "Spam content" };

      var result = await _service.ApplyAsync(readerUser.UserId, adminUser.UserId, request);

      result.Message.Should().Contain("cảnh báo");
    }

    [Fact]
    public async Task ApplyAsync_Cannot_Self_Deactivate()
    {
      var adminUser = await CreateUserWithRolesAsync(1, "admin1", 5);

      var request = new AccountActionRequest { Action = AccountActionType.DEACTIVATE, Reason = "Testing" };

      var act = async () => await _service.ApplyAsync(adminUser.UserId, adminUser.UserId, request);
      await act.Should().ThrowAsync<InvalidOperationException>()
          .WithMessage("*chính mình*");
    }

    // ============================================================
    // TESTS: UpdateRolesAsync — Role assignment hierarchy
    // ============================================================

    [Fact]
    public async Task UpdateRolesAsync_Moderator_Cannot_Assign_Admin_Role()
    {
      var readerUser = await CreateUserWithRolesAsync(1, "reader1", 1);
      var modUser = await CreateUserWithRolesAsync(2, "mod1", 4);

      using var serviceContext = _fixture.CreateDbContext();
      var freshService = new AccountModerationService(serviceContext);

      var request = new UpdateUserRolesRequest { Roles = new List<string> { "READER", "ADMIN" } };

      var act = async () => await freshService.UpdateRolesAsync(readerUser.UserId, modUser.UserId, request);
      await act.Should().ThrowAsync<UnauthorizedAccessException>()
          .WithMessage("*cấp quyền Hệ thống Quản trị*");
    }

    [Fact]
    public async Task UpdateRolesAsync_Moderator_Cannot_Assign_Moderator_Role()
    {
      var readerUser = await CreateUserWithRolesAsync(1, "reader1", 1);
      var modUser = await CreateUserWithRolesAsync(2, "mod1", 4);

      using var serviceContext = _fixture.CreateDbContext();
      var freshService = new AccountModerationService(serviceContext);

      var request = new UpdateUserRolesRequest { Roles = new List<string> { "READER", "MODERATOR" } };

      var act = async () => await freshService.UpdateRolesAsync(readerUser.UserId, modUser.UserId, request);
      await act.Should().ThrowAsync<UnauthorizedAccessException>()
          .WithMessage("*cấp quyền Hệ thống Quản trị*");
    }

    [Fact]
    public async Task UpdateRolesAsync_Moderator_Cannot_Edit_Admin_Roles()
    {
      var adminUser = await CreateUserWithRolesAsync(1, "admin1", 5);
      var adminBackup = await CreateUserWithRolesAsync(3, "admin_backup", 5); // Ensures 'last admin' check won't trigger
      var modUser = await CreateUserWithRolesAsync(2, "mod1", 4);

      // Use a fresh context for the service to avoid tracking conflicts
      using var serviceContext = _fixture.CreateDbContext();
      var freshService = new AccountModerationService(serviceContext);

      var request = new UpdateUserRolesRequest { Roles = new List<string> { "READER" } };

      var act = async () => await freshService.UpdateRolesAsync(adminUser.UserId, modUser.UserId, request);
      await act.Should().ThrowAsync<UnauthorizedAccessException>()
          .WithMessage("*thay đổi vai trò*");
    }

    [Fact]
    public async Task UpdateRolesAsync_Admin_Cannot_Edit_Another_Admin_Roles()
    {
      var admin1 = await CreateUserWithRolesAsync(1, "admin1", 5);
      var admin2 = await CreateUserWithRolesAsync(2, "admin2", 5);

      using var serviceContext = _fixture.CreateDbContext();
      var freshService = new AccountModerationService(serviceContext);

      var request = new UpdateUserRolesRequest { Roles = new List<string> { "READER" } };

      var act = async () => await freshService.UpdateRolesAsync(admin1.UserId, admin2.UserId, request);
      await act.Should().ThrowAsync<UnauthorizedAccessException>()
          .WithMessage("*Admin không thể*");
    }

    [Fact]
    public async Task UpdateRolesAsync_Admin_Can_Add_Roles_To_Regular_User()
    {
      var readerUser = await CreateUserWithRolesAsync(1, "reader1", 1);
      var adminUser = await CreateUserWithRolesAsync(2, "admin1", 5);

      // Use a fresh context for the service to avoid tracking conflicts
      using var serviceContext = _fixture.CreateDbContext();
      var freshService = new AccountModerationService(serviceContext);

      var request = new UpdateUserRolesRequest { Roles = new List<string> { "READER", "CREATOR", "TRANSLATOR" } };

      var result = await freshService.UpdateRolesAsync(readerUser.UserId, adminUser.UserId, request);

      result.Should().BeTrue();

      // Use yet another fresh context to verify DB state
      using var verifyContext = _fixture.CreateDbContext();
      var updatedRoles = await verifyContext.UserRoles
          .Include(ur => ur.Role)
          .Where(ur => ur.UserId == readerUser.UserId)
          .Select(ur => ur.Role.RoleName)
          .ToListAsync();

      updatedRoles.Should().HaveCount(3);
      updatedRoles.Should().Contain(RoleName.READER);
      updatedRoles.Should().Contain(RoleName.CREATOR);
      updatedRoles.Should().Contain(RoleName.TRANSLATOR);
    }

    [Fact]
    public async Task UpdateRolesAsync_Cannot_Remove_Last_Admin()
    {
      var adminUser = await CreateUserWithRolesAsync(1, "admin1", 5);

      // Use a fresh context for the service to avoid tracking conflicts
      using var serviceContext = _fixture.CreateDbContext();
      var freshService = new AccountModerationService(serviceContext);

      var request = new UpdateUserRolesRequest { Roles = new List<string> { "READER" } };

      var act = async () => await freshService.UpdateRolesAsync(adminUser.UserId, adminUser.UserId, request);
      await act.Should().ThrowAsync<InvalidOperationException>()
          .WithMessage("*ít nhất một Admin*");
    }
  }
}
