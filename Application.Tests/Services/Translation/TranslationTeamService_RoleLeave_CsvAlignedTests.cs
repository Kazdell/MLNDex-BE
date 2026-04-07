using System;
using System.Threading.Tasks;
using Application.DTOs.Translation;
using Application.Interfaces.Common;
using Application.Interfaces.Notification;
using Application.Services.Translation;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.Translation;

public class TranslationTeamService_RoleLeave_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;
  private readonly Mock<IUserContext> _mockUserContext = new();
  private readonly Mock<INotificationService> _mockNotificationService = new();

  public TranslationTeamService_RoleLeave_CsvAlignedTests(ITestOutputHelper output)
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
    db.Users.Add(new User { UserId = 1, Username = "leader", DisplayName = "Leader", Email = "leader@test.com", PasswordHash = "hash" });
    db.Users.Add(new User { UserId = 2, Username = "member", DisplayName = "Member", Email = "member@test.com", PasswordHash = "hash" });
    db.Users.Add(new User { UserId = 3, Username = "user3", DisplayName = "User3", Email = "u3@test.com", PasswordHash = "hash" });
    db.TranslationTeams.Add(new TranslationTeam { TeamId = 1, LeaderId = 1, TeamName = "Team RL", Slug = "team-rl" });
    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task RemoveMemberAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(1);

    db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 2, Role = TeamMemberRole.TRANSLATOR, IsActive = true, JoinedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.RemoveMemberAsync(1, 2);

    _output.WriteLine("Input: leader remove member userId=2");
    _output.WriteLine($"Output: {output}");

    output.Should().BeTrue();
    (await db.TeamMembers.AnyAsync(m => m.TeamId == 1 && m.UserId == 2)).Should().BeFalse();
    _mockNotificationService.Verify(x => x.CreateNotificationAsync(2, "Team RL", "Bạn đã bị gỡ khỏi nhóm", "/teams", NotificationType.TEAM_MEMBER_REMOVED), Times.Once);
  }

  [Fact]
  public async Task RemoveMemberAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(1);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var ex = await Assert.ThrowsAsync<Exception>(() => service.RemoveMemberAsync(-1, 2));

    _output.WriteLine("Input: teamId=-1");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Be("Team not found or unauthorized.");
  }

  [Fact]
  public async Task RemoveMemberAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(1);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var ex = await Assert.ThrowsAsync<Exception>(() => service.RemoveMemberAsync(1, 1));

    _output.WriteLine("Input: target user is leader");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Be("Leader cannot be removed.");
  }

  [Fact]
  public async Task LeaveTeamAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(2);

    db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 2, Role = TeamMemberRole.TRANSLATOR, IsActive = true, JoinedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.LeaveTeamAsync(1);

    _output.WriteLine("Input: member userId=2 leave teamId=1");
    _output.WriteLine($"Output: {output}");

    output.Should().BeTrue();
    (await db.TeamMembers.FirstAsync(m => m.TeamId == 1 && m.UserId == 2)).IsActive.Should().BeFalse();
    _mockNotificationService.Verify(x => x.CreateNotificationAsync(1, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), NotificationType.TEAM_MEMBER_LEFT), Times.Once);
  }

  [Fact]
  public async Task LeaveTeamAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(null as int?);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LeaveTeamAsync(1));

    _output.WriteLine("Input: userId=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task LeaveTeamAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(1);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var ex = await Assert.ThrowsAsync<Exception>(() => service.LeaveTeamAsync(1));

    _output.WriteLine("Input: leader tries to leave");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Contain("Trưởng nhóm không thể rời nhóm");
  }

  [Fact]
  public async Task AssignRoleAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(1);

    db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 2, Role = TeamMemberRole.TRANSLATOR, IsActive = true, JoinedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.AssignRoleAsync(1, 2, new AssignTeamMemberRoleDto { Role = TeamMemberRole.EDITOR });

    _output.WriteLine("Input: assign EDITOR to member userId=2");
    _output.WriteLine($"Output role={output.Role}");

    output.Role.Should().Be(TeamMemberRole.EDITOR.ToString());
    _mockNotificationService.Verify(x => x.CreateNotificationAsync(2, "Team RL", It.IsAny<string>(), "/teams/1/members", NotificationType.TEAM_ROLE_CHANGED), Times.Once);
  }

  [Fact]
  public async Task AssignRoleAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(1);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var ex = await Assert.ThrowsAsync<Exception>(() => service.AssignRoleAsync(-1, 2, new AssignTeamMemberRoleDto { Role = TeamMemberRole.EDITOR }));

    _output.WriteLine("Input: teamId=-1");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Be("Team not found or unauthorized.");
  }

  [Fact]
  public async Task AssignRoleAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(1);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var ex = await Assert.ThrowsAsync<Exception>(() => service.AssignRoleAsync(1, 1, new AssignTeamMemberRoleDto { Role = TeamMemberRole.EDITOR }));

    _output.WriteLine("Input: try changing leader role");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Be("Leader role cannot be changed manually.");
  }
}
