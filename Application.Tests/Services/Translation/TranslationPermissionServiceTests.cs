using System;
using System.Linq;
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

namespace Application.Tests.Services.Translation
{
  public class TranslationPermissionServiceTests
  {
    private readonly Mock<IUserContext> _mockUserContext;
    private readonly Mock<INotificationService> _mockNotificationService;

    public TranslationPermissionServiceTests()
    {
      _mockUserContext = new Mock<IUserContext>();
      _mockNotificationService = new Mock<INotificationService>();
    }

    private MlndexDbContext CreateInMemoryDbContext()
    {
      var options = new DbContextOptionsBuilder<MlndexDbContext>()
          .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
          .Options;

      return new MlndexDbContext(options);
    }

    [Fact]
    public async Task RequestPermissionAsync_ShouldThrow_WhenUserNotActiveMember()
    {
      var db = CreateInMemoryDbContext();
      _mockUserContext.Setup(u => u.UserId).Returns(123);

      db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 999, IsActive = true, JoinedAt = DateTime.UtcNow });
      await db.SaveChangesAsync();

      var service = new TranslationPermissionService(db, _mockUserContext.Object, _mockNotificationService.Object);
      var dto = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 1, Note = "Hello" };

      var ex = await Assert.ThrowsAsync<Exception>(() => service.RequestPermissionAsync(dto));
      ex.Message.Should().Be("You are not an active member of this translation team.");
    }

    [Fact]
    public async Task RequestPermissionAsync_ShouldCreatePermission_AndNotifyCreator_OnSuccess()
    {
      var db = CreateInMemoryDbContext();
      _mockUserContext.Setup(u => u.UserId).Returns(123);

      db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      db.TranslationTeams.Add(new TranslationTeam { TeamId = 1, TeamName = "Hero Team", Slug = "hero-team" });
      db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 5, UserId = 456, PenName = "Author" });
      db.Series.Add(new Series { SeriesId = 10, CreatorId = 5, Title = "Epic Manga" });
      db.Languages.Add(new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" });
      await db.SaveChangesAsync();

      var service = new TranslationPermissionService(db, _mockUserContext.Object, _mockNotificationService.Object);
      var dto = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 1, Note = "Please let us translate" };

      var result = await service.RequestPermissionAsync(dto);

      result.Should().NotBeNull();
      result.Status.Should().Be(TranslationPermissionStatus.PENDING.ToString());

      var permissionInDb = await db.TranslationPermissions.FirstOrDefaultAsync(p => p.TeamId == 1 && p.SeriesId == 10);
      permissionInDb.Should().NotBeNull();
      permissionInDb?.Note.Should().Be("Please let us translate");

      _mockNotificationService.Verify(n => n.CreateNotificationAsync(
          456,
          "Yêu cầu dịch truyện mới",
          It.Is<string>(s => s.Contains("Hero Team") && s.Contains("Epic Manga")),
          "/creator/translation-requests",
          NotificationType.TRANSLATION_REQUEST
      ), Times.Once);
    }

    [Fact]
    public async Task ReviewPermissionAsync_ShouldThrow_WhenNotCreator()
    {
      var db = CreateInMemoryDbContext();
      _mockUserContext.Setup(u => u.UserId).Returns(999); // wrong user

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
      ex.Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task ReviewPermissionAsync_ShouldUpdateStatusToGranted_AndNotifyTeam()
    {
      var db = CreateInMemoryDbContext();
      _mockUserContext.Setup(u => u.UserId).Returns(456);

      db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 5, UserId = 456, PenName = "Author" });
      db.Series.Add(new Series { SeriesId = 10, CreatorId = 5, Title = "Epic Manga" });
      db.Languages.Add(new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" });
      db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 111, IsActive = true, JoinedAt = DateTime.UtcNow });
      db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 222, IsActive = true, JoinedAt = DateTime.UtcNow });

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

      var result = await service.ReviewPermissionAsync(99, new ReviewPermissionDto { IsApproved = true });

      result.Status.Should().Be(TranslationPermissionStatus.GRANTED.ToString());

      var permissionInDb = await db.TranslationPermissions.FindAsync(99);
      permissionInDb?.Status.Should().Be(TranslationPermissionStatus.GRANTED);
      permissionInDb?.GrantedAt.Should().NotBeNull();

      _mockNotificationService.Verify(n => n.CreateNotificationAsync(
          It.IsAny<int>(), // user ID validated below via Times.Exactly
          "Yêu cầu dịch truyện được chấp thuận",
          It.Is<string>(s => s.Contains("Epic Manga") && s.Contains("Vietnamese")),
          "/translation/sent-requests/1",
          NotificationType.TRANSLATION_GRANTED
      ), Times.Exactly(2));
    }

    [Fact]
    public async Task RequestPermissionAsync_ShouldThrow_WhenPermissionAlreadyExists()
    {
      var db = CreateInMemoryDbContext();
      _mockUserContext.Setup(u => u.UserId).Returns(123);

      db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
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
      var dto = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 1, Note = "Retry" };

      var ex = await Assert.ThrowsAsync<Exception>(() => service.RequestPermissionAsync(dto));
      ex.Message.Should().Be("A permission request for this series and language already exists.");
    }

    [Fact]
    public async Task ReviewPermissionAsync_ShouldUpdateStatusToDenied_AndNotifyTeam()
    {
      var db = CreateInMemoryDbContext();
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
      var result = await service.ReviewPermissionAsync(99, new ReviewPermissionDto { IsApproved = false });

      result.Status.Should().Be(TranslationPermissionStatus.DENIED.ToString());

      var permissionInDb = await db.TranslationPermissions.FindAsync(99);
      permissionInDb?.Status.Should().Be(TranslationPermissionStatus.DENIED);
      permissionInDb?.RevokedAt.Should().NotBeNull();

      _mockNotificationService.Verify(n => n.CreateNotificationAsync(
          111,
          "Yêu cầu dịch truyện bị từ chối",
          It.Is<string>(s => s.Contains("Epic Manga") && s.Contains("Vietnamese")),
          "/translation/sent-requests/1",
          NotificationType.TRANSLATION_REVOKED
      ), Times.Once);
    }
  }
}
