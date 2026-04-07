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

public class TranslationTeamService_Membership_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;
  private readonly Mock<IUserContext> _mockUserContext = new();
  private readonly Mock<INotificationService> _mockNotificationService = new();

  public TranslationTeamService_Membership_CsvAlignedTests(ITestOutputHelper output)
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
    db.Users.Add(new User { UserId = 3, Username = "other", DisplayName = "Other", Email = "other@test.com", PasswordHash = "hash" });
    db.TranslationTeams.Add(new TranslationTeam { TeamId = 1, LeaderId = 1, TeamName = "Team One", Slug = "team-one" });
    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task InviteMemberAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(1);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var input = new InviteTeamMemberDto { UserId = 2, Role = TeamMemberRole.TRANSLATOR };

    var output = await service.InviteMemberAsync(1, input);

    _output.WriteLine("Input: teamId=1, invitee=2, role=TRANSLATOR");
    _output.WriteLine($"Output invitationId={output}");

    output.Should().BeGreaterThan(0);
    _mockNotificationService.Verify(x => x.CreateNotificationAsync(2, "Team One", It.IsAny<string>(), "/teams/1", NotificationType.TEAM_INVITATION), Times.Once);
  }

  [Fact]
  public async Task InviteMemberAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(3);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAsync<Exception>(() => service.InviteMemberAsync(-1, new InviteTeamMemberDto { UserId = 2, Role = TeamMemberRole.TRANSLATOR }));

    _output.WriteLine("Input: teamId=-1 non-owner");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Be("Team not found or unauthorized.");
  }

  [Fact]
  public async Task InviteMemberAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Throws(new Exception("User context failure"));

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAsync<Exception>(() => service.InviteMemberAsync(1, new InviteTeamMemberDto { UserId = 2, Role = TeamMemberRole.TRANSLATOR }));

    _output.WriteLine("Input: user context throws");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Be("User context failure");
  }

  [Fact]
  public async Task AcceptInvitationAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(2);

    db.TeamInvitations.Add(new TeamInvitation
    {
      InvitationId = 21,
      TeamId = 1,
      InviteeId = 2,
      InviterId = 1,
      Role = TeamMemberRole.EDITOR.ToString(),
      Status = TeamInvitationStatus.PENDING,
      CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.AcceptInvitationAsync(21);

    _output.WriteLine("Input: invitationId=21 accepted by invitee=2");
    _output.WriteLine($"Output: {output}");

    output.Should().BeTrue();
    (await db.TeamMembers.AnyAsync(m => m.TeamId == 1 && m.UserId == 2 && m.Role == TeamMemberRole.EDITOR)).Should().BeTrue();
    _mockNotificationService.Verify(x => x.CreateNotificationAsync(1, It.IsAny<string>(), It.IsAny<string>(), "/teams/1/members", NotificationType.TEAM_INVITATION_ACCEPTED), Times.Once);
  }

  [Fact]
  public async Task AcceptInvitationAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(2);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.AcceptInvitationAsync(-1);

    _output.WriteLine("Input: invitationId=-1");
    _output.WriteLine($"Output: {output}");

    output.Should().BeFalse();
  }

  [Fact]
  public async Task AcceptInvitationAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(null as int?);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.AcceptInvitationAsync(1));

    _output.WriteLine("Input: userId=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task RejectInvitationAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(2);

    db.TeamInvitations.Add(new TeamInvitation
    {
      InvitationId = 31,
      TeamId = 1,
      InviteeId = 2,
      InviterId = 1,
      Role = TeamMemberRole.TRANSLATOR.ToString(),
      Status = TeamInvitationStatus.PENDING,
      CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.RejectInvitationAsync(31);

    _output.WriteLine("Input: invitationId=31 rejected by invitee=2");
    _output.WriteLine($"Output: {output}");

    output.Should().BeTrue();
    (await db.TeamInvitations.FindAsync(31))!.Status.Should().Be(TeamInvitationStatus.REJECTED);
    _mockNotificationService.Verify(x => x.CreateNotificationAsync(1, It.IsAny<string>(), It.IsAny<string>(), "/teams/1/members", NotificationType.TEAM_INVITATION_REJECTED), Times.Once);
  }

  [Fact]
  public async Task RejectInvitationAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(2);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.RejectInvitationAsync(-1);

    _output.WriteLine("Input: invitationId=-1");
    _output.WriteLine($"Output: {output}");

    output.Should().BeFalse();
  }

  [Fact]
  public async Task RejectInvitationAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Throws(new Exception("User context failure"));

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAsync<Exception>(() => service.RejectInvitationAsync(1));

    _output.WriteLine("Input: user context throws");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Be("User context failure");
  }
}
