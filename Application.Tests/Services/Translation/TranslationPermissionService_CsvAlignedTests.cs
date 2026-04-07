using System;
using System.Threading.Tasks;
using Application.DTOs.Notification;
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

public class TranslationPermissionService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;
  private readonly Mock<IUserContext> _mockUserContext = new();
  private readonly Mock<INotificationService> _mockNotificationService = new();

  public TranslationPermissionService_CsvAlignedTests(ITestOutputHelper output)
  {
    _output = output;
  }

  private TranslationPermissionService CreateService(MlndexDbContext db)
  {
    _mockNotificationService
      .Setup(x => x.CreateNotificationAsync(
        It.IsAny<int>(),
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<NotificationType>()))
      .ReturnsAsync(new NotificationDto
      {
        NotificationId = 1,
        Title = "ok",
        Message = "ok"
      });

    return new TranslationPermissionService(db, _mockUserContext.Object, _mockNotificationService.Object);
  }

  private static MlndexDbContext CreateInMemoryDbContext()
  {
    var options = new DbContextOptionsBuilder<MlndexDbContext>()
        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
        .Options;

    return new MlndexDbContext(options);
  }

  [Fact]
  public async Task RequestPermissionAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    _mockUserContext.Setup(u => u.UserId).Returns(123);

    db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
    db.TranslationTeams.Add(new TranslationTeam { TeamId = 1, TeamName = "Hero Team", Slug = "hero-team" });
    db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 5, UserId = 456, PenName = "Author" });
    db.Series.Add(new Series { SeriesId = 10, CreatorId = 5, Title = "Epic Manga" });
    db.Languages.Add(new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" });
    await db.SaveChangesAsync();

    var service = new TranslationPermissionService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var input = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 1, Note = "Please let us translate" };

    var output = await service.RequestPermissionAsync(input);

    _output.WriteLine($"Input: TeamId={input.TeamId}, SeriesId={input.SeriesId}, LanguageId={input.LanguageId}");
    _output.WriteLine($"Output: PermissionId={output.PermissionId}, Status={output.Status}");

    output.Should().NotBeNull();
    output.Status.Should().Be(TranslationPermissionStatus.PENDING.ToString());
  }

  [Fact]
  public async Task RequestPermissionAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    _mockUserContext.Setup(u => u.UserId).Returns(123);

    db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 999, IsActive = true, JoinedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    var service = new TranslationPermissionService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var input = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 1, Note = "Invalid membership" };

    var ex = await Assert.ThrowsAsync<Exception>(() => service.RequestPermissionAsync(input));
    _output.WriteLine($"Input: TeamId={input.TeamId}, UserId=123");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("You are not an active member of this translation team.");
  }

  [Fact]
  public async Task RequestPermissionAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    _mockUserContext.Setup(u => u.UserId).Returns(123);

    var service = new TranslationPermissionService(db, _mockUserContext.Object, _mockNotificationService.Object);

    // Null dto is a hard invalid path and must throw from service flow.
    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.RequestPermissionAsync(null!));
    _output.WriteLine($"Input: dto=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task RequestPermissionAsync_TC04_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    _mockUserContext.Setup(u => u.UserId).Returns(123);

    db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    var service = new TranslationPermissionService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var input = new RequestPermissionDto { TeamId = 1, SeriesId = 404, LanguageId = 1, Note = "Missing series" };

    var ex = await Assert.ThrowsAsync<Exception>(() => service.RequestPermissionAsync(input));
    _output.WriteLine($"Input: SeriesId={input.SeriesId}");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("Series not found.");
  }

  [Fact]
  public async Task RequestPermissionAsync_TC05_BusinessRule()
  {
    await using var db = CreateInMemoryDbContext();
    _mockUserContext.Setup(u => u.UserId).Returns(123);

    db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
    db.TranslationTeams.Add(new TranslationTeam { TeamId = 1, TeamName = "Hero Team", Slug = "hero-team" });
    db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 5, UserId = 456, PenName = "Author" });
    db.Series.Add(new Series { SeriesId = 10, CreatorId = 5, Title = "Epic Manga" });
    db.Languages.Add(new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" });
    db.TranslationPermissions.Add(new TranslationPermission
    {
      SeriesId = 10,
      TeamId = 1,
      LanguageId = 1,
      Status = TranslationPermissionStatus.PENDING,
      Origin = PermissionOrigin.REQUESTED_BY_TEAM
    });
    await db.SaveChangesAsync();

    var service = new TranslationPermissionService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var input = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 1, Note = "Duplicate pending" };

    var ex = await Assert.ThrowsAsync<Exception>(() => service.RequestPermissionAsync(input));
    _output.WriteLine($"Input: duplicate pending request");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("Yêu cầu dịch truyện của nhóm cho bộ này đang chờ xử lý.");
  }

  [Fact]
  public async Task ReviewPermissionAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    _mockUserContext.Setup(u => u.UserId).Returns(456);

    db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 5, UserId = 456, PenName = "Author" });
    db.Series.Add(new Series { SeriesId = 10, CreatorId = 5, Title = "Epic Manga" });
    db.Languages.Add(new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" });
    db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 111, IsActive = true, JoinedAt = DateTime.UtcNow });
    db.TranslationPermissions.Add(new TranslationPermission
    {
      PermissionId = 99,
      SeriesId = 10,
      TeamId = 1,
      LanguageId = 1,
      Status = TranslationPermissionStatus.PENDING,
      Origin = PermissionOrigin.REQUESTED_BY_TEAM
    });
    await db.SaveChangesAsync();

    var service = new TranslationPermissionService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.ReviewPermissionAsync(99, new ReviewPermissionDto { IsApproved = true });

    _output.WriteLine("Input: permissionId=99, IsApproved=true");
    _output.WriteLine($"Output: Status={output.Status}");

    output.Status.Should().Be(TranslationPermissionStatus.GRANTED.ToString());
  }

  [Fact]
  public async Task ReviewPermissionAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    _mockUserContext.Setup(u => u.UserId).Returns(456);

    var service = new TranslationPermissionService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAsync<Exception>(() => service.ReviewPermissionAsync(-1, new ReviewPermissionDto { IsApproved = true }));
    _output.WriteLine("Input: permissionId=-1");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("Permission request not found.");
  }

  [Fact]
  public async Task ReviewPermissionAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    _mockUserContext.Setup(u => u.UserId).Returns(null as int?);

    var service = new TranslationPermissionService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReviewPermissionAsync(1, new ReviewPermissionDto { IsApproved = true }));
    _output.WriteLine("Input: creator user context null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task ReviewPermissionAsync_TC04_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    _mockUserContext.Setup(u => u.UserId).Returns(456);

    var service = new TranslationPermissionService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAsync<Exception>(() => service.ReviewPermissionAsync(404, new ReviewPermissionDto { IsApproved = false }));
    _output.WriteLine("Input: permissionId=404");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("Permission request not found.");
  }

  [Fact]
  public async Task ReviewPermissionAsync_TC05_BusinessRule()
  {
    await using var db = CreateInMemoryDbContext();
    _mockUserContext.Setup(u => u.UserId).Returns(999);

    db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 5, UserId = 456, PenName = "Author" });
    db.Series.Add(new Series { SeriesId = 10, CreatorId = 5, Title = "Epic Manga" });
    db.TranslationPermissions.Add(new TranslationPermission
    {
      PermissionId = 1,
      SeriesId = 10,
      TeamId = 1,
      LanguageId = 1,
      Status = TranslationPermissionStatus.PENDING,
      Origin = PermissionOrigin.REQUESTED_BY_TEAM
    });
    await db.SaveChangesAsync();

    var service = new TranslationPermissionService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAsync<Exception>(() => service.ReviewPermissionAsync(1, new ReviewPermissionDto { IsApproved = true }));
    _output.WriteLine("Input: unauthorized reviewer");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("Unauthorized");
  }

  [Fact]
  public async Task GetTeamPermissionsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();

    db.TranslationTeams.Add(new TranslationTeam { TeamId = 1, TeamName = "Team A", Slug = "team-a", Facebook = "fb/a" });
    db.Series.Add(new Series { SeriesId = 10, CreatorId = 5, Title = "Series A" });
    db.Languages.Add(new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" });
    db.TranslationPermissions.AddRange(
      new TranslationPermission { PermissionId = 1, SeriesId = 10, TeamId = 1, LanguageId = 1, GrantedBy = 456, Status = TranslationPermissionStatus.PENDING, Origin = PermissionOrigin.REQUESTED_BY_TEAM },
      new TranslationPermission { PermissionId = 2, SeriesId = 10, TeamId = 1, LanguageId = 1, GrantedBy = 456, Status = TranslationPermissionStatus.GRANTED, Origin = PermissionOrigin.REQUESTED_BY_TEAM, GrantedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetTeamPermissionsAsync(1);

    _output.WriteLine("Input: teamId=1");
    _output.WriteLine($"Output: count={output.Count()}");

    output.Should().HaveCount(2);
    output.First().PermissionId.Should().Be(2);
  }

  [Fact]
  public async Task GetTeamPermissionsAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = CreateService(db);

    var output = await service.GetTeamPermissionsAsync(-1);

    _output.WriteLine("Input: teamId=-1");
    _output.WriteLine($"Output: count={output.Count()}");

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetTeamPermissionsAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetTeamPermissionsAsync(1));

    _output.WriteLine("Input: disposed db context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetCreatorPermissionsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();

    db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 5, UserId = 456, PenName = "Author" });
    db.TranslationTeams.Add(new TranslationTeam { TeamId = 1, TeamName = "Team A", Slug = "team-a" });
    db.Series.Add(new Series { SeriesId = 10, CreatorId = 5, Title = "Series A" });
    db.Languages.Add(new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" });
    db.TranslationPermissions.Add(new TranslationPermission
    {
      PermissionId = 10,
      SeriesId = 10,
      TeamId = 1,
      LanguageId = 1,
      GrantedBy = 456,
      Status = TranslationPermissionStatus.PENDING,
      Origin = PermissionOrigin.REQUESTED_BY_TEAM
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetCreatorPermissionsAsync(456);

    _output.WriteLine("Input: userId=456 creator");
    _output.WriteLine($"Output: count={output.Count()}");

    output.Should().HaveCount(1);
    output.First().PermissionId.Should().Be(10);
  }

  [Fact]
  public async Task GetCreatorPermissionsAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = CreateService(db);

    var output = await service.GetCreatorPermissionsAsync(-1);

    _output.WriteLine("Input: userId=-1");
    _output.WriteLine($"Output: count={output.Count()}");

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetCreatorPermissionsAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetCreatorPermissionsAsync(1));

    _output.WriteLine("Input: disposed db context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task AutoDenyExpiredRequestsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();

    db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 5, UserId = 456, PenName = "Author" });
    db.Series.Add(new Series { SeriesId = 10, CreatorId = 5, Title = "Series A" });
    db.TranslationTeams.Add(new TranslationTeam { TeamId = 1, TeamName = "Team A", Slug = "team-a" });
    db.Languages.Add(new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" });
    db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
    db.TranslationPermissions.Add(new TranslationPermission
    {
      PermissionId = 30,
      SeriesId = 10,
      TeamId = 1,
      LanguageId = 1,
      GrantedBy = 456,
      Status = TranslationPermissionStatus.PENDING,
      Origin = PermissionOrigin.REQUESTED_BY_TEAM,
      CreatedAt = DateTime.UtcNow.AddHours(-100)
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.AutoDenyExpiredRequestsAsync(72);

    _output.WriteLine("Input: expireHours=72 with one expired pending request");
    _output.WriteLine($"Output: deniedCount={output}");

    output.Should().Be(1);
    var updated = await db.TranslationPermissions.FirstAsync(p => p.PermissionId == 30);
    updated.Status.Should().Be(TranslationPermissionStatus.DENIED);
    updated.Note.Should().Contain("Tự động từ chối");
  }

  [Fact]
  public async Task AutoDenyExpiredRequestsAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();

    db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 5, UserId = 456, PenName = "Author" });
    db.Series.Add(new Series { SeriesId = 10, CreatorId = 5, Title = "Series A" });
    db.TranslationTeams.Add(new TranslationTeam { TeamId = 1, TeamName = "Team A", Slug = "team-a" });
    db.Languages.Add(new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" });
    db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
    db.TranslationPermissions.Add(new TranslationPermission
    {
      PermissionId = 31,
      SeriesId = 10,
      TeamId = 1,
      LanguageId = 1,
      GrantedBy = 456,
      Status = TranslationPermissionStatus.PENDING,
      Origin = PermissionOrigin.REQUESTED_BY_TEAM,
      CreatedAt = DateTime.UtcNow.AddHours(-1)
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.AutoDenyExpiredRequestsAsync(-1);

    _output.WriteLine("Input: expireHours=-1 (invalid edge)");
    _output.WriteLine($"Output: deniedCount={output}");

    output.Should().Be(1);
  }

  [Fact]
  public async Task AutoDenyExpiredRequestsAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.AutoDenyExpiredRequestsAsync());

    _output.WriteLine("Input: disposed db context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }
}
