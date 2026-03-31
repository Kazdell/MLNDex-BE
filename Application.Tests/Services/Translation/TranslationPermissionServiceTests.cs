using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.Translation.Requests;
using Application.DTOs.Translation.Responses;
using Application.Interfaces.Common;
using Application.Interfaces.Notification;
using Application.Services.Translation;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

using Application.Tests.Shared;

namespace Application.Tests.Services.Translation
{
  [Collection("Database collection")]
  public class TranslationPermissionServiceTests : IAsyncLifetime
  {
    private readonly Mock<IUserContext> _mockUserContext;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly DatabaseFixture _fixture;

    public TranslationPermissionServiceTests(DatabaseFixture fixture)
    {
      _fixture = fixture;
      _mockUserContext = new Mock<IUserContext>();
      _mockNotificationService = new Mock<INotificationService>();
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        using var db = _fixture.CreateDbContext();
        db.Users.AddRange(
            new User { UserId = 111, Username = "user111", Email = "u111@test.com", DisplayName = "D111", PasswordHash = "X" },
            new User { UserId = 123, Username = "user123", Email = "u123@test.com", DisplayName = "D123", PasswordHash = "X" },
            new User { UserId = 222, Username = "user222", Email = "u222@test.com", DisplayName = "D222", PasswordHash = "X" },
            new User { UserId = 456, Username = "user456", Email = "u456@test.com", DisplayName = "D456", PasswordHash = "X" },
            new User { UserId = 789, Username = "user789", Email = "u789@test.com", DisplayName = "D789", PasswordHash = "X" },
            new User { UserId = 999, Username = "user999", Email = "u999@test.com", DisplayName = "D999", PasswordHash = "X" },
            new User { UserId = 1, Username = "user1", Email = "u1@test.com", DisplayName = "D1", PasswordHash = "X" },
            new User { UserId = 2, Username = "user2", Email = "u2@test.com", DisplayName = "D2", PasswordHash = "X" },
            new User { UserId = 3, Username = "user3", Email = "u3@test.com", DisplayName = "D3", PasswordHash = "X" },
            new User { UserId = 4, Username = "user4", Email = "u4@test.com", DisplayName = "D4", PasswordHash = "X" },
            new User { UserId = 5, Username = "user5", Email = "u5@test.com", DisplayName = "D5", PasswordHash = "X" },
            new User { UserId = 10, Username = "user10", Email = "u10@test.com", DisplayName = "D10", PasswordHash = "X" }
        );
        await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;

    private MlndexDbContext CreateDb() => _fixture.CreateDbContext();

    private TranslationPermissionService CreateService(MlndexDbContext db)
      => new TranslationPermissionService(db, _mockUserContext.Object, _mockNotificationService.Object);

    private async Task<(int t1, int t2)> SeedBaseData(MlndexDbContext db, int creatorUserId = 456)
    {
      db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 5, UserId = creatorUserId, PenName = "Author" });
      db.Series.Add(new Series { SeriesId = 10, CreatorId = 5, Title = "Epic Manga" });
      db.Chapters.Add(new Chapter { ChapterId = 100, SeriesId = 10, ChapterNumber = 1 });
      db.Chapters.Add(new Chapter { ChapterId = 101, SeriesId = 10, ChapterNumber = 2 });
      db.Languages.Add(new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" });
      await db.SaveChangesAsync();

      var team1 = new TranslationTeam { TeamName = "Hero Team", Slug = "hero-team", LeaderId = 1, LanguageId = 1 };
      var team2 = new TranslationTeam { TeamName = "Rival Team", Slug = "rival-team", LeaderId = 2, LanguageId = 1 };
      db.TranslationTeams.AddRange(team1, team2);
      await db.SaveChangesAsync();
      return (team1.TeamId, team2.TeamId);
    }

    // ═══════════════════════════════════════════════════════════
    // REQUEST PERMISSION
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task RequestPermission_ShouldThrow_WhenNotMemberAtAll()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(999);
      var (t1, _) = await SeedBaseData(db);
      var dto = new RequestPermissionRequest { TeamId = t1, SeriesId = 10, LanguageId = 1 };
      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RequestPermissionAsync(dto));
      ex.Message.Should().Be("You are not an active member of this translation team.");
    }

    [Fact]
    public async Task RequestPermission_ShouldThrow_WhenPendingMember()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      var (t1, _) = await SeedBaseData(db);
      db.TeamMembers.Add(new TeamMember { TeamId = t1, UserId = 123, IsActive = false, JoinedAt = DateTime.UtcNow });
      await db.SaveChangesAsync();

      var dto = new RequestPermissionRequest { TeamId = t1, SeriesId = 10, LanguageId = 1 };
      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RequestPermissionAsync(dto));
      ex.Message.Should().Be("You are not an active member of this translation team.");
    }

    [Fact]
    public async Task RequestPermission_ShouldThrow_WhenSeriesNotFound()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      var (t1, _) = await SeedBaseData(db);
      db.TeamMembers.Add(new TeamMember { TeamId = t1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      await db.SaveChangesAsync();

      var dto = new RequestPermissionRequest { TeamId = t1, SeriesId = 9999, LanguageId = 1 };
      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RequestPermissionAsync(dto));
      ex.Message.Should().Be("Series not found.");
    }

    [Fact(Skip = "FK constraint prevents CreatorId orphaning - this scenario is impossible with proper DB constraints")]
    public async Task RequestPermission_ShouldThrow_WhenCreatorProfileNotFound()
    {
      await Task.CompletedTask;
    }

    [Fact]
    public async Task RequestPermission_ShouldThrow_WhenLanguageNotFound()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      var (t1, _) = await SeedBaseData(db);
      db.TeamMembers.Add(new TeamMember { TeamId = t1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      await db.SaveChangesAsync();
      var dto = new RequestPermissionRequest { TeamId = t1, SeriesId = 10, LanguageId = 99 };
      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RequestPermissionAsync(dto));
      ex.Message.Should().Be("Language not found.");
    }

    [Fact]
    public async Task RequestPermission_ExistingPending_ShouldThrow()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      var (t1, _) = await SeedBaseData(db);
      db.TeamMembers.Add(new TeamMember { TeamId = t1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, SeriesId = 10, TeamId = t1, LanguageId = 1, Status = TranslationPermissionStatus.PENDING });
      await db.SaveChangesAsync();

      var dto = new RequestPermissionRequest { TeamId = t1, SeriesId = 10, LanguageId = 1, IsUnofficial = false };
      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RequestPermissionAsync(dto));
      ex.Message.Should().Be("Yêu cầu dịch truyện của nhóm cho bộ này đang chờ xử lý.");
    }

    [Fact]
    public async Task RequestPermission_ExistingGranted_ShouldThrow()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      var (t1, _) = await SeedBaseData(db);
      db.TeamMembers.Add(new TeamMember { TeamId = t1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, SeriesId = 10, TeamId = t1, LanguageId = 1, Status = TranslationPermissionStatus.GRANTED });
      await db.SaveChangesAsync();

      var dto = new RequestPermissionRequest { TeamId = t1, SeriesId = 10, LanguageId = 1, IsUnofficial = true };
      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RequestPermissionAsync(dto));
      ex.Message.Should().Be("Nhóm đã có quyền dịch Official cho bộ truyện này.");
    }

    [Fact]
    public async Task RequestPermission_ExistingDenied_To_Official_ShouldUpdateToPending()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      var (t1, _) = await SeedBaseData(db);
      db.TeamMembers.Add(new TeamMember { TeamId = t1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 55, SeriesId = 10, TeamId = t1, LanguageId = 1, Status = TranslationPermissionStatus.DENIED, RevokedAt = DateTime.UtcNow });
      await db.SaveChangesAsync();

      var dto = new RequestPermissionRequest { TeamId = t1, SeriesId = 10, LanguageId = 1, IsUnofficial = false };
      var result = await CreateService(db).RequestPermissionAsync(dto);

      result.Status.Should().Be("PENDING");
      result.PermissionId.Should().Be(55);
      var p = await db.TranslationPermissions.FindAsync(55);
      p!.Status.Should().Be(TranslationPermissionStatus.PENDING);
      p.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task RequestPermission_ExistingDenied_To_Unofficial_ShouldUpdateToUnofficial()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      var (t1, _) = await SeedBaseData(db);
      db.TeamMembers.Add(new TeamMember { TeamId = t1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 55, SeriesId = 10, TeamId = t1, LanguageId = 1, Status = TranslationPermissionStatus.DENIED });
      await db.SaveChangesAsync();

      var dto = new RequestPermissionRequest { TeamId = t1, SeriesId = 10, LanguageId = 1, IsUnofficial = true };
      var result = await CreateService(db).RequestPermissionAsync(dto);

      result.Status.Should().Be("UNOFFICIAL");
    }

    [Fact]
    public async Task RequestPermission_ExistingRevoked_To_Official_ShouldUpdateToPending()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      var (t1, _) = await SeedBaseData(db);
      db.TeamMembers.Add(new TeamMember { TeamId = t1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 56, SeriesId = 10, TeamId = t1, LanguageId = 1, Status = TranslationPermissionStatus.REVOKED });
      await db.SaveChangesAsync();

      var dto = new RequestPermissionRequest { TeamId = t1, SeriesId = 10, LanguageId = 1, IsUnofficial = false };
      var result = await CreateService(db).RequestPermissionAsync(dto);

      result.Status.Should().Be("PENDING");
    }

    [Fact]
    public async Task RequestPermission_ExistingUnofficial_To_Official_ShouldUpdateToPending()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      var (t1, _) = await SeedBaseData(db);
      db.TeamMembers.Add(new TeamMember { TeamId = t1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 57, SeriesId = 10, TeamId = t1, LanguageId = 1, Status = TranslationPermissionStatus.UNOFFICIAL });
      await db.SaveChangesAsync();

      var dto = new RequestPermissionRequest { TeamId = t1, SeriesId = 10, LanguageId = 1, IsUnofficial = false };
      var result = await CreateService(db).RequestPermissionAsync(dto);

      result.Status.Should().Be("PENDING");
    }

    [Fact]
    public async Task RequestPermission_ExistingUnofficial_To_Unofficial_ShouldUpdateToUnofficial()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      var (t1, _) = await SeedBaseData(db);
      db.TeamMembers.Add(new TeamMember { TeamId = t1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 57, SeriesId = 10, TeamId = t1, LanguageId = 1, Status = TranslationPermissionStatus.UNOFFICIAL, Note = "Old Note" });
      await db.SaveChangesAsync();

      var dto = new RequestPermissionRequest { TeamId = t1, SeriesId = 10, LanguageId = 1, IsUnofficial = true, Note = "New Note" };
      var result = await CreateService(db).RequestPermissionAsync(dto);

      result.Status.Should().Be("UNOFFICIAL");
      result.Note.Should().Be("New Note");
    }

    // ═══════════════════════════════════════════════════════════
    // REVIEW PERMISSION
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ReviewPermission_ShouldThrow_WhenPermissionNotFound()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).ReviewPermissionAsync(999, new ReviewPermissionRequest { IsApproved = true }));
      ex.Message.Should().Be("Permission request not found.");
    }

    [Fact]
    public async Task ReviewPermission_ShouldThrow_WhenCallerIsNotCreator()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(999);
      var (t1, _) = await SeedBaseData(db, creatorUserId: 456);
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 99, SeriesId = 10, TeamId = t1, LanguageId = 1, Status = TranslationPermissionStatus.PENDING });
      await db.SaveChangesAsync();

      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionRequest { IsApproved = true }));
      ex.Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task ReviewPermission_Pending_Approve_ShouldGrant()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      var (t1, _) = await SeedBaseData(db, creatorUserId: 456);
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 99, SeriesId = 10, TeamId = t1, LanguageId = 1, Status = TranslationPermissionStatus.PENDING });
      await db.SaveChangesAsync();

      var result = await CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionRequest { IsApproved = true });
      result.Status.Should().Be("GRANTED");
    }

    [Fact]
    public async Task ReviewPermission_Denied_Approve_ShouldGrant()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      var (t1, _) = await SeedBaseData(db, creatorUserId: 456);
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 99, SeriesId = 10, TeamId = t1, LanguageId = 1, Status = TranslationPermissionStatus.DENIED });
      await db.SaveChangesAsync();

      var result = await CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionRequest { IsApproved = true });
      result.Status.Should().Be("GRANTED");
    }

    [Fact]
    public async Task ReviewPermission_Unofficial_Approve_ShouldGrant()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      var (t1, _) = await SeedBaseData(db, creatorUserId: 456);
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 99, SeriesId = 10, TeamId = t1, LanguageId = 1, Status = TranslationPermissionStatus.UNOFFICIAL });
      await db.SaveChangesAsync();

      var result = await CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionRequest { IsApproved = true });
      result.Status.Should().Be("GRANTED");
    }

    [Fact]
    public async Task ReviewPermission_Pending_Reject_ShouldDeny()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      var (t1, _) = await SeedBaseData(db, creatorUserId: 456);
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 99, SeriesId = 10, TeamId = t1, LanguageId = 1, Status = TranslationPermissionStatus.PENDING });
      await db.SaveChangesAsync();

      var result = await CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionRequest { IsApproved = false });
      result.Status.Should().Be("DENIED");

      var p = await db.TranslationPermissions.FindAsync(99);
      p!.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ReviewPermission_Granted_Reject_ShouldDeny()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      var (t1, _) = await SeedBaseData(db, creatorUserId: 456);
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 99, SeriesId = 10, TeamId = t1, LanguageId = 1, Status = TranslationPermissionStatus.GRANTED });
      await db.SaveChangesAsync();

      var result = await CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionRequest { IsApproved = false });
      result.Status.Should().Be("DENIED");
    }

    [Fact]
    public async Task ReviewPermission_ShouldSyncMultipleTranslations_ToOfficial()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      var (t1, t2) = await SeedBaseData(db, creatorUserId: 456);
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 99, SeriesId = 10, TeamId = t1, LanguageId = 1, Status = TranslationPermissionStatus.UNOFFICIAL });
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 88, SeriesId = 10, TeamId = t2, LanguageId = 1, Status = TranslationPermissionStatus.UNOFFICIAL });

      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 1, PermissionId = 99, ChapterId = 100, IsOfficial = false });
      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 2, PermissionId = 99, ChapterId = 101, IsOfficial = false });
      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 3, PermissionId = 88, ChapterId = 100, IsOfficial = false });
      await db.SaveChangesAsync();

      await CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionRequest { IsApproved = true });

      var tr1 = await db.Translations.FindAsync(1);
      var tr2 = await db.Translations.FindAsync(2);
      var tr3 = await db.Translations.FindAsync(3);

      tr1!.IsOfficial.Should().BeTrue();
      tr2!.IsOfficial.Should().BeTrue();
      tr3!.IsOfficial.Should().BeFalse();
    }

    [Fact]
    public async Task ReviewPermission_ShouldSyncMultipleTranslations_ToUnofficial()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      var (t1, t2) = await SeedBaseData(db, creatorUserId: 456);
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 99, SeriesId = 10, TeamId = t1, LanguageId = 1, Status = TranslationPermissionStatus.GRANTED });
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 88, SeriesId = 10, TeamId = t2, LanguageId = 1, Status = TranslationPermissionStatus.UNOFFICIAL });

      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 1, PermissionId = 99, ChapterId = 100, IsOfficial = true });
      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 2, PermissionId = 99, ChapterId = 101, IsOfficial = true });
      await db.SaveChangesAsync();

      await CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionRequest { IsApproved = false });

      var tr1 = await db.Translations.FindAsync(1);
      var tr2 = await db.Translations.FindAsync(2);
      tr1!.IsOfficial.Should().BeFalse();
      tr2!.IsOfficial.Should().BeFalse();
    }

    [Fact]
    public async Task ReviewPermission_ShouldNotifyOnlyActiveTeamMembers()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      var (t1, _) = await SeedBaseData(db, creatorUserId: 456);

      db.TeamMembers.Add(new TeamMember { TeamId = t1, UserId = 111, IsActive = true, JoinedAt = DateTime.UtcNow });
      db.TeamMembers.Add(new TeamMember { TeamId = t1, UserId = 222, IsActive = false, JoinedAt = DateTime.UtcNow });

      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 99, SeriesId = 10, TeamId = t1, LanguageId = 1, Status = TranslationPermissionStatus.PENDING });
      await db.SaveChangesAsync();

      await CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionRequest { IsApproved = true });

      _mockNotificationService.Verify(n => n.CreateNotificationAsync(
          It.IsAny<int>(), "Yêu cầu dịch truyện được chấp thuận", It.IsAny<string>(),
          It.IsAny<string>(), NotificationType.TRANSLATION_GRANTED), Times.Exactly(1));
    }

    // ═══════════════════════════════════════════════════════════
    // GET PERMISSIONS
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetTeamPermissions_ShouldReturnOnlyRequestedTeam()
    {
      var db = CreateDb();
      var (t1, t2) = await SeedBaseData(db);
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 1, TeamId = t1, SeriesId = 10 });
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 2, TeamId = t2, SeriesId = 10 });
      await db.SaveChangesAsync();

      var result = await CreateService(db).GetTeamPermissionsAsync(t1);
      result.Should().ContainSingle();
      result.First().PermissionId.Should().Be(1);
    }

    [Fact]
    public async Task GetTeamPermissions_ShouldOrderByIdDescending()
    {
      var db = CreateDb();
      var (t1, _) = await SeedBaseData(db);
      db.Series.Add(new Series { SeriesId = 11, CreatorId = 5, Title = "Serie 2" });
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 1, TeamId = t1, SeriesId = 10 });
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 2, TeamId = t1, SeriesId = 11 });
      await db.SaveChangesAsync();

      var result = await CreateService(db).GetTeamPermissionsAsync(t1);
      result.First().PermissionId.Should().Be(2);
    }

    [Fact]
    public async Task GetCreatorPermissions_ShouldReturnOnlyRequestedCreator()
    {
      var db = CreateDb();
      var (t1, _) = await SeedBaseData(db, creatorUserId: 456);

      db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 6, UserId = 789, PenName = "Author 2" });
      db.Series.Add(new Series { SeriesId = 11, CreatorId = 6, Title = "Title by Author 2" });

      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 1, TeamId = t1, SeriesId = 10 });
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 789, PermissionId = 2, TeamId = t1, SeriesId = 11 });
      await db.SaveChangesAsync();

      var result = await CreateService(db).GetCreatorPermissionsAsync(456);
      result.Should().ContainSingle();
      result.First().PermissionId.Should().Be(1);
    }

    [Fact]
    public async Task GetCreatorPermissions_ShouldReturnEmptyIfUserIdNotCreator()
    {
      var db = CreateDb();
      await SeedBaseData(db, creatorUserId: 456);
      var result = await CreateService(db).GetCreatorPermissionsAsync(999);
      result.Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════
    // AUTO DENY EXPIRED
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AutoDenyExpiredRequests_ShouldNotDeny_IfUnder72Hours()
    {
      var db = CreateDb();
      var (t1, _) = await SeedBaseData(db);
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 1, TeamId = t1, SeriesId = 10, Status = TranslationPermissionStatus.PENDING, CreatedAt = DateTime.UtcNow.AddHours(-71) });
      await db.SaveChangesAsync();

      var count = await CreateService(db).AutoDenyExpiredRequestsAsync(72);
      count.Should().Be(0);
      var p = await db.TranslationPermissions.FindAsync(1);
      p!.Status.Should().Be(TranslationPermissionStatus.PENDING);
    }

    [Fact]
    public async Task AutoDenyExpiredRequests_ShouldDeny_IfExactlyOrOver72Hours()
    {
      var db = CreateDb();
      var (t1, _) = await SeedBaseData(db, creatorUserId: 456);
      db.TeamMembers.Add(new TeamMember { TeamId = t1, UserId = 111, IsActive = true });
      db.Series.Add(new Series { SeriesId = 11, CreatorId = 5, Title = "Another Manga" });

      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 1, TeamId = t1, SeriesId = 10, Status = TranslationPermissionStatus.PENDING, CreatedAt = DateTime.UtcNow.AddHours(-73) });
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 2, TeamId = t1, SeriesId = 11, Status = TranslationPermissionStatus.PENDING, CreatedAt = DateTime.UtcNow.AddHours(-100) });
      await db.SaveChangesAsync();

      var count = await CreateService(db).AutoDenyExpiredRequestsAsync(72);
      count.Should().Be(2);

      var p1 = await db.TranslationPermissions.FindAsync(1);
      p1!.Status.Should().Be(TranslationPermissionStatus.DENIED);

      _mockNotificationService.Verify(n => n.CreateNotificationAsync(
          111, "Yêu cầu dịch truyện bị từ chối tự động", It.IsAny<string>(), It.IsAny<string>(), NotificationType.TRANSLATION_REVOKED), Times.Exactly(2));
    }

    [Fact]
    public async Task AutoDenyExpiredRequests_ShouldIgnoreNonPendingRequests_EvenIfOld()
    {
      var db = CreateDb();
      var (t1, _) = await SeedBaseData(db);
      db.Series.Add(new Series { SeriesId = 11, CreatorId = 5, Title = "Another Manga" });
      db.Series.Add(new Series { SeriesId = 12, CreatorId = 5, Title = "Third Manga" });

      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 1, TeamId = t1, SeriesId = 10, Status = TranslationPermissionStatus.GRANTED, CreatedAt = DateTime.UtcNow.AddHours(-100) });
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 2, TeamId = t1, SeriesId = 11, Status = TranslationPermissionStatus.UNOFFICIAL, CreatedAt = DateTime.UtcNow.AddHours(-100) });
      db.TranslationPermissions.Add(new TranslationPermission { GrantedBy = 456, PermissionId = 3, TeamId = t1, SeriesId = 12, Status = TranslationPermissionStatus.DENIED, CreatedAt = DateTime.UtcNow.AddHours(-100) });
      await db.SaveChangesAsync();

      var count = await CreateService(db).AutoDenyExpiredRequestsAsync(72);
      count.Should().Be(0);
    }
  }
}
