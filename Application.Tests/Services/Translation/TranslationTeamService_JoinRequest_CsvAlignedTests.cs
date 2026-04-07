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

public class TranslationTeamService_JoinRequest_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;
  private readonly Mock<IUserContext> _mockUserContext = new();
  private readonly Mock<INotificationService> _mockNotificationService = new();

  public TranslationTeamService_JoinRequest_CsvAlignedTests(ITestOutputHelper output)
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
    db.Users.Add(new User { UserId = 2, Username = "requester", DisplayName = "Requester", Email = "requester@test.com", PasswordHash = "hash" });
    db.TranslationTeams.Add(new TranslationTeam { TeamId = 1, LeaderId = 1, TeamName = "Join Team", Slug = "join-team" });
    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task RequestToJoinAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(2);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.RequestToJoinAsync(1, new JoinTeamRequestDto { Message = "Please accept me" });

    _output.WriteLine("Input: userId=2 request teamId=1");
    _output.WriteLine($"Output requestId={output}");

    output.Should().BeGreaterThan(0);
    _mockNotificationService.Verify(x => x.CreateNotificationAsync(1, "Join Team", It.IsAny<string>(), "/teams/1/requests", NotificationType.TEAM_JOIN_REQUEST), Times.Once);
  }

  [Fact]
  public async Task RequestToJoinAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(null as int?);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RequestToJoinAsync(1, new JoinTeamRequestDto { Message = "x" }));

    _output.WriteLine("Input: userId=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task RequestToJoinAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Throws(new Exception("User context failure"));

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAsync<Exception>(() => service.RequestToJoinAsync(1, new JoinTeamRequestDto { Message = "x" }));

    _output.WriteLine("Input: user context throws");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Be("User context failure");
  }

  [Fact]
  public async Task ApproveJoinRequestAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(1);

    db.TeamJoinRequests.Add(new TeamJoinRequest
    {
      RequestId = 61,
      TeamId = 1,
      UserId = 2,
      Message = "Please approve",
      Status = TeamJoinRequestStatus.PENDING,
      CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.ApproveJoinRequestAsync(61);

    _output.WriteLine("Input: leader approves requestId=61");
    _output.WriteLine($"Output: {output}");

    output.Should().BeTrue();
    (await db.TeamMembers.AnyAsync(m => m.TeamId == 1 && m.UserId == 2 && m.IsActive)).Should().BeTrue();
    _mockNotificationService.Verify(x => x.CreateNotificationAsync(2, "Join Team", "Yêu cầu tham gia nhóm của bạn đã được phê duyệt", "/teams/1", NotificationType.TEAM_JOIN_APPROVED), Times.Once);
  }

  [Fact]
  public async Task ApproveJoinRequestAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(1);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.ApproveJoinRequestAsync(-1);

    _output.WriteLine("Input: requestId=-1");
    _output.WriteLine($"Output: {output}");

    output.Should().BeFalse();
  }

  [Fact]
  public async Task ApproveJoinRequestAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(1);

    db.TeamMembers.Add(new TeamMember
    {
      TeamId = 99,
      UserId = 2,
      IsActive = false,
      LeftAt = DateTime.UtcNow.AddHours(-1),
      JoinedAt = DateTime.UtcNow.AddDays(-2),
      Role = TeamMemberRole.TRANSLATOR
    });
    db.TeamJoinRequests.Add(new TeamJoinRequest
    {
      RequestId = 63,
      TeamId = 1,
      UserId = 2,
      Message = "Need to join",
      Status = TeamJoinRequestStatus.PENDING,
      CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAsync<Exception>(() => service.ApproveJoinRequestAsync(63));

    _output.WriteLine("Input: cooldown active on approving user");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Contain("Bạn phải chờ");
  }

  [Fact]
  public async Task RejectJoinRequestAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(1);

    db.TeamJoinRequests.Add(new TeamJoinRequest
    {
      RequestId = 71,
      TeamId = 1,
      UserId = 2,
      Message = "Please do not accept",
      Status = TeamJoinRequestStatus.PENDING,
      CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.RejectJoinRequestAsync(71);

    _output.WriteLine("Input: leader rejects requestId=71");
    _output.WriteLine($"Output: {output}");

    output.Should().BeTrue();
    (await db.TeamJoinRequests.FindAsync(71))!.Status.Should().Be(TeamJoinRequestStatus.REJECTED);
    _mockNotificationService.Verify(x => x.CreateNotificationAsync(2, "Join Team", "Yêu cầu tham gia nhóm của bạn đã bị từ chối", "/teams/1", NotificationType.TEAM_JOIN_REJECTED), Times.Once);
  }

  [Fact]
  public async Task RejectJoinRequestAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Returns(1);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.RejectJoinRequestAsync(-1);

    _output.WriteLine("Input: requestId=-1");
    _output.WriteLine($"Output: {output}");

    output.Should().BeFalse();
  }

  [Fact]
  public async Task RejectJoinRequestAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    _mockUserContext.Setup(x => x.UserId).Throws(new Exception("User context failure"));

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAsync<Exception>(() => service.RejectJoinRequestAsync(1));

    _output.WriteLine("Input: user context throws");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Be("User context failure");
  }
}
