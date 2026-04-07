using System;
using System.Linq;
using System.Threading.Tasks;
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

public class TranslationTeamService_Query_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;
  private readonly Mock<IUserContext> _mockUserContext = new();
  private readonly Mock<INotificationService> _mockNotificationService = new();

  public TranslationTeamService_Query_CsvAlignedTests(ITestOutputHelper output)
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

  private TranslationTeamService CreateService(MlndexDbContext db)
    => new(db, _mockUserContext.Object, _mockNotificationService.Object);

  private static async Task SeedQueryBaseAsync(MlndexDbContext db)
  {
    db.Users.AddRange(
      new User { UserId = 1, Username = "leader", DisplayName = "Leader", Email = "leader@test.com", PasswordHash = "hash" },
      new User { UserId = 2, Username = "member", DisplayName = "Member", Email = "member@test.com", PasswordHash = "hash" },
      new User { UserId = 3, Username = "other", DisplayName = "Other", Email = "other@test.com", PasswordHash = "hash" });

    db.Languages.AddRange(
      new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" },
      new Language { LanguageId = 2, Name = "English", Code = "en" });

    db.TranslationTeams.AddRange(
      new TranslationTeam { TeamId = 1, LeaderId = 1, TeamName = "Team One", Slug = "team-one" },
      new TranslationTeam { TeamId = 2, LeaderId = 3, TeamName = "Team Two", Slug = "team-two" },
      new TranslationTeam { TeamId = 9, LeaderId = 1, TeamName = "Leader Team No Member", Slug = "leader-only" });

    db.TeamMembers.AddRange(
      new TeamMember { MembershipId = 1, TeamId = 1, UserId = 1, Role = TeamMemberRole.LEADER, IsActive = true, JoinedAt = DateTime.UtcNow.AddDays(-10) },
      new TeamMember { MembershipId = 2, TeamId = 1, UserId = 2, Role = TeamMemberRole.TRANSLATOR, IsActive = true, JoinedAt = DateTime.UtcNow.AddDays(-8) },
      new TeamMember { MembershipId = 3, TeamId = 1, UserId = 3, Role = TeamMemberRole.EDITOR, IsActive = false, JoinedAt = DateTime.UtcNow.AddDays(-7), LeftAt = DateTime.UtcNow.AddDays(-1) });

    db.Series.Add(new Series { SeriesId = 10, CreatorId = 1, Title = "Series A", AverageRating = 4.4m });

    db.TranslationPermissions.AddRange(
      new TranslationPermission { PermissionId = 101, TeamId = 1, SeriesId = 10, LanguageId = 1, Status = TranslationPermissionStatus.GRANTED, Origin = PermissionOrigin.REQUESTED_BY_TEAM },
      new TranslationPermission { PermissionId = 102, TeamId = 1, SeriesId = 10, LanguageId = 2, Status = TranslationPermissionStatus.PENDING, Origin = PermissionOrigin.REQUESTED_BY_TEAM },
      new TranslationPermission { PermissionId = 103, TeamId = 1, SeriesId = 10, LanguageId = 2, Status = TranslationPermissionStatus.DENIED, Origin = PermissionOrigin.REQUESTED_BY_TEAM });

    db.Translations.AddRange(
      new Domain.Entities.Translation { TranslationId = 5001, ChapterId = 1, PermissionId = 101, LanguageId = 1, ContentType = ContentType.TEXT, PublishedAt = DateTime.UtcNow.AddDays(-2) },
      new Domain.Entities.Translation { TranslationId = 5002, ChapterId = 1, PermissionId = 101, LanguageId = 1, ContentType = ContentType.TEXT, PublishedAt = DateTime.UtcNow.AddDays(-1) });

    db.TeamInvitations.AddRange(
      new TeamInvitation { InvitationId = 201, TeamId = 1, InviteeId = 2, InviterId = 1, Role = TeamMemberRole.EDITOR.ToString(), Status = TeamInvitationStatus.PENDING, CreatedAt = DateTime.UtcNow.AddDays(-2) },
      new TeamInvitation { InvitationId = 202, TeamId = 1, InviteeId = 3, InviterId = 1, Role = TeamMemberRole.TRANSLATOR.ToString(), Status = TeamInvitationStatus.ACCEPTED, CreatedAt = DateTime.UtcNow.AddDays(-1) });

    db.TeamJoinRequests.AddRange(
      new TeamJoinRequest { RequestId = 301, TeamId = 1, UserId = 2, Message = "join", Status = TeamJoinRequestStatus.PENDING, CreatedAt = DateTime.UtcNow.AddDays(-1) },
      new TeamJoinRequest { RequestId = 302, TeamId = 1, UserId = 3, Message = "joined", Status = TeamJoinRequestStatus.REJECTED, CreatedAt = DateTime.UtcNow.AddDays(-1) });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task GetTeamInvitationsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedQueryBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(1);
    var service = CreateService(db);

    var output = (await service.GetTeamInvitationsAsync(1)).ToList();

    _output.WriteLine("Input: leaderId=1, teamId=1");
    _output.WriteLine($"Output: Count={output.Count}, FirstInvitation={output.FirstOrDefault()?.InvitationId}");

    output.Should().HaveCount(1);
    output[0].InvitationId.Should().Be(201);
  }

  [Fact]
  public async Task GetTeamInvitationsAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedQueryBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(null as int?);
    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetTeamInvitationsAsync(1));

    _output.WriteLine("Input: leaderId=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetTeamInvitationsAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    _mockUserContext.Setup(x => x.UserId).Returns(1);
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetTeamInvitationsAsync(1));

    _output.WriteLine("Input: disposed context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetTeamJoinRequestsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedQueryBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(1);
    var service = CreateService(db);

    var output = (await service.GetTeamJoinRequestsAsync(1)).ToList();

    _output.WriteLine("Input: leaderId=1, teamId=1");
    _output.WriteLine($"Output: Count={output.Count}, FirstRequest={output.FirstOrDefault()?.RequestId}");

    output.Should().HaveCount(1);
    output[0].RequestId.Should().Be(301);
  }

  [Fact]
  public async Task GetTeamJoinRequestsAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedQueryBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(null as int?);
    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetTeamJoinRequestsAsync(1));

    _output.WriteLine("Input: leaderId=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetTeamJoinRequestsAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    _mockUserContext.Setup(x => x.UserId).Returns(1);
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetTeamJoinRequestsAsync(1));

    _output.WriteLine("Input: disposed context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetTeamMembersAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedQueryBaseAsync(db);
    var service = CreateService(db);

    var output = (await service.GetTeamMembersAsync(1)).ToList();

    _output.WriteLine("Input: teamId=1");
    _output.WriteLine($"Output: Count={output.Count}");

    output.Should().HaveCount(3);
  }

  [Fact]
  public async Task GetTeamMembersAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedQueryBaseAsync(db);
    var service = CreateService(db);

    var output = (await service.GetTeamMembersAsync(-1)).ToList();

    _output.WriteLine("Input: teamId=-1");
    _output.WriteLine($"Output: Count={output.Count}");

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetTeamMembersAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetTeamMembersAsync(1));

    _output.WriteLine("Input: disposed context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetTeamSeriesAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedQueryBaseAsync(db);
    var service = CreateService(db);

    var output = (await service.GetTeamSeriesAsync(1)).ToList();

    _output.WriteLine("Input: teamId=1");
    _output.WriteLine($"Output: Count={output.Count}, FirstStatus={output.FirstOrDefault()?.Status}");

    output.Should().HaveCount(3);
  }

  [Fact]
  public async Task GetTeamSeriesAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedQueryBaseAsync(db);
    var service = CreateService(db);

    var output = (await service.GetTeamSeriesAsync(-1)).ToList();

    _output.WriteLine("Input: teamId=-1");
    _output.WriteLine($"Output: Count={output.Count}");

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetTeamSeriesAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetTeamSeriesAsync(1));

    _output.WriteLine("Input: disposed context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetTeamStatsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedQueryBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetTeamStatsAsync(1);

    _output.WriteLine("Input: teamId=1");
    _output.WriteLine($"Output: ActiveSeries={output.ActiveSeriesCount}, Chapters={output.TotalChaptersTranslated}");

    output.ActiveSeriesCount.Should().Be(1);
    output.TotalChaptersTranslated.Should().Be(2);
  }

  [Fact]
  public async Task GetTeamStatsAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedQueryBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetTeamStatsAsync(-1);

    _output.WriteLine("Input: teamId=-1");
    _output.WriteLine($"Output: ActiveSeries={output.ActiveSeriesCount}, Chapters={output.TotalChaptersTranslated}");

    output.ActiveSeriesCount.Should().Be(0);
    output.TotalChaptersTranslated.Should().Be(0);
  }

  [Fact]
  public async Task GetTeamStatsAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetTeamStatsAsync(1));

    _output.WriteLine("Input: disposed context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetUserTeamsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedQueryBaseAsync(db);
    var service = CreateService(db);

    var output = (await service.GetUserTeamsAsync(2)).ToList();

    _output.WriteLine("Input: userId=2 active member");
    _output.WriteLine($"Output: Count={output.Count}, FirstRole={output.FirstOrDefault()?.Role}");

    output.Should().NotBeEmpty();
    output[0].Role.Should().Be(TeamMemberRole.TRANSLATOR.ToString());
  }

  [Fact]
  public async Task GetUserTeamsAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedQueryBaseAsync(db);
    var service = CreateService(db);

    var output = (await service.GetUserTeamsAsync(-1)).ToList();

    _output.WriteLine("Input: userId=-1");
    _output.WriteLine($"Output: Count={output.Count}");

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetUserTeamsAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetUserTeamsAsync(1));

    _output.WriteLine("Input: disposed context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }
}
