using System;
using System.Collections.Generic;
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
            new Domain.Entities.User { UserId = 111, Username = "user111", Email = "u111@test.com", DisplayName = "D111", PasswordHash = "X" },
            new Domain.Entities.User { UserId = 456, Username = "user456", Email = "u456@test.com", DisplayName = "D456", PasswordHash = "X" },
            new Domain.Entities.User { UserId = 789, Username = "user789", Email = "u789@test.com", DisplayName = "D789", PasswordHash = "X" },
            new Domain.Entities.User { UserId = 999, Username = "user999", Email = "u999@test.com", DisplayName = "D999", PasswordHash = "X" },
            new Domain.Entities.User { UserId = 1, Username = "user1", Email = "u1@test.com", DisplayName = "D1", PasswordHash = "X" },
            new Domain.Entities.User { UserId = 2, Username = "user2", Email = "u2@test.com", DisplayName = "D2", PasswordHash = "X" },
            new Domain.Entities.User { UserId = 3, Username = "user3", Email = "u3@test.com", DisplayName = "D3", PasswordHash = "X" },
            new Domain.Entities.User { UserId = 4, Username = "user4", Email = "u4@test.com", DisplayName = "D4", PasswordHash = "X" },
            new Domain.Entities.User { UserId = 5, Username = "user5", Email = "u5@test.com", DisplayName = "D5", PasswordHash = "X" },
            new Domain.Entities.User { UserId = 10, Username = "user10", Email = "u10@test.com", DisplayName = "D10", PasswordHash = "X" }
        );
        await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;

    private MlndexDbContext CreateDb()
    {
      return _fixture.CreateDbContext();
    }

    private TranslationPermissionService CreateService(MlndexDbContext db)
      => new TranslationPermissionService(db, _mockUserContext.Object, _mockNotificationService.Object);

    private async Task SeedBaseData(MlndexDbContext db, int creatorUserId = 456)
    {
      // Phase 1: Seed independent entities first
      db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 5, UserId = creatorUserId, PenName = "Author" });
      db.Series.Add(new Series { SeriesId = 10, CreatorId = 5, Title = "Epic Manga" });
      db.Chapters.Add(new Chapter { ChapterId = 100, SeriesId = 10, ChapterNumber = 1 });
      db.Languages.Add(new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" });
      await db.SaveChangesAsync();

      // Phase 2: Seed entities with FK dependencies on Language and User
      db.TranslationTeams.Add(new TranslationTeam { TeamId = 1, TeamName = "Hero Team", Slug = "hero-team", LeaderId = 1, LanguageId = 1 });
      db.TranslationTeams.Add(new TranslationTeam { TeamId = 2, TeamName = "Rival Team", Slug = "rival-team", LeaderId = 2, LanguageId = 1 });
      await db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════
    // TPERMO_REQ: MATRIX CÁC TRƯỜNG HỢP XIN QUYỀN (15 Cases)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Xin quyền dịch - Ném ngoại lệ (Block) - Khi Not Member At All
    /// </summary>
    public async Task RequestPermission_ShouldThrow_WhenNotMemberAtAll()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(999); 
      await SeedBaseData(db);
      var dto = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 1 };
      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RequestPermissionAsync(dto));
      ex.Message.Should().Be("You are not an active member of this translation team.");
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Xin quyền dịch - Ném ngoại lệ (Block) - Khi Pending Member
    /// </summary>
    public async Task RequestPermission_ShouldThrow_WhenPendingMember()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      await SeedBaseData(db);
      db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = false, JoinedAt = DateTime.UtcNow }); // Inactive member
      await db.SaveChangesAsync();

      var dto = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 1 };
      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RequestPermissionAsync(dto));
      ex.Message.Should().Be("You are not an active member of this translation team.");
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Xin quyền dịch - Ném ngoại lệ (Block) - Khi Series không tồn tại
    /// </summary>
    public async Task RequestPermission_ShouldThrow_WhenSeriesNotFound()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      await db.SaveChangesAsync();
      var dto = new RequestPermissionDto { TeamId = 1, SeriesId = 9999, LanguageId = 1 };
      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RequestPermissionAsync(dto));
      ex.Message.Should().Be("Series not found.");
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Xin quyền dịch - Ném ngoại lệ (Block) - Khi Creator Profile không tồn tại
    /// </summary>
    public async Task RequestPermission_ShouldThrow_WhenCreatorProfileNotFound()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      db.Series.Add(new Series { SeriesId = 10, CreatorId = 99, Title = "No Creator Manga" });
      await db.SaveChangesAsync();
      var dto = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 1 };
      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RequestPermissionAsync(dto));
      ex.Message.Should().Be("Creator profile not found for this series.");
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Xin quyền dịch - Ném ngoại lệ (Block) - Khi Language không tồn tại
    /// </summary>
    public async Task RequestPermission_ShouldThrow_WhenLanguageNotFound()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      await SeedBaseData(db);
      var dto = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 99 };
      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RequestPermissionAsync(dto));
      ex.Message.Should().Be("Language not found.");
    }

    // -- State Transition Matrix: Existing Status vs Requested Status --

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Xin quyền dịch - Existing Pending - Ném ngoại lệ (Block)
    /// </summary>
    public async Task RequestPermission_ExistingPending_ShouldThrow()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      await SeedBaseData(db);
      db.TranslationPermissions.Add(new TranslationPermission { SeriesId = 10, TeamId = 1, LanguageId = 1, Status = TranslationPermissionStatus.PENDING });
      await db.SaveChangesAsync();

      var dto = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 1, IsUnofficial = false };
      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RequestPermissionAsync(dto));
      ex.Message.Should().Be("Yêu cầu dịch truyện của nhóm cho bộ này đang chờ xử lý.");
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Xin quyền dịch - Existing Granted - Ném ngoại lệ (Block)
    /// </summary>
    public async Task RequestPermission_ExistingGranted_ShouldThrow()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      await SeedBaseData(db);
      db.TranslationPermissions.Add(new TranslationPermission { SeriesId = 10, TeamId = 1, LanguageId = 1, Status = TranslationPermissionStatus.GRANTED });
      await db.SaveChangesAsync();

      var dto = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 1, IsUnofficial = true };
      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RequestPermissionAsync(dto));
      ex.Message.Should().Be("Nhóm đã có quyền dịch Official cho bộ truyện này.");
    }

    // DENIED Transitions
    [Fact]
    /// <summary>
    /// Kiểm tra logic: Xin quyền dịch - Existing Denied - To - Official - Cập nhật To Pending
    /// </summary>
    public async Task RequestPermission_ExistingDenied_To_Official_ShouldUpdateToPending()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      await SeedBaseData(db);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 55, SeriesId = 10, TeamId = 1, LanguageId = 1, Status = TranslationPermissionStatus.DENIED, RevokedAt = DateTime.UtcNow });
      await db.SaveChangesAsync();

      var dto = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 1, IsUnofficial = false };
      var result = await CreateService(db).RequestPermissionAsync(dto);

      result.Status.Should().Be("PENDING");
      result.PermissionId.Should().Be(55); 
      var p = await db.TranslationPermissions.FindAsync(55);
      p!.Status.Should().Be(TranslationPermissionStatus.PENDING);
      p.RevokedAt.Should().BeNull(); // Re-requested, so RevokedAt clears
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Xin quyền dịch - Existing Denied - To - Unofficial - Cập nhật To Unofficial
    /// </summary>
    public async Task RequestPermission_ExistingDenied_To_Unofficial_ShouldUpdateToUnofficial()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      await SeedBaseData(db);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 55, SeriesId = 10, TeamId = 1, LanguageId = 1, Status = TranslationPermissionStatus.DENIED });
      await db.SaveChangesAsync();

      var dto = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 1, IsUnofficial = true };
      var result = await CreateService(db).RequestPermissionAsync(dto);

      result.Status.Should().Be("UNOFFICIAL");
      var p = await db.TranslationPermissions.FindAsync(55);
      p!.Status.Should().Be(TranslationPermissionStatus.UNOFFICIAL);
      p.GrantedAt.Should().NotBeNull();
    }

    // REVOKED Transitions (Author took back rights, team asks again)
    [Fact]
    /// <summary>
    /// Kiểm tra logic: Xin quyền dịch - Existing Revoked - To - Official - Cập nhật To Pending
    /// </summary>
    public async Task RequestPermission_ExistingRevoked_To_Official_ShouldUpdateToPending()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      await SeedBaseData(db);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 56, SeriesId = 10, TeamId = 1, LanguageId = 1, Status = TranslationPermissionStatus.REVOKED });
      await db.SaveChangesAsync();

      var dto = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 1, IsUnofficial = false };
      var result = await CreateService(db).RequestPermissionAsync(dto);

      result.Status.Should().Be("PENDING");
    }

    // UNOFFICIAL Transitions (Team upgrading to official)
    [Fact]
    /// <summary>
    /// Kiểm tra logic: Xin quyền dịch - Existing Unofficial - To - Official - Cập nhật To Pending
    /// </summary>
    public async Task RequestPermission_ExistingUnofficial_To_Official_ShouldUpdateToPending()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      await SeedBaseData(db);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 57, SeriesId = 10, TeamId = 1, LanguageId = 1, Status = TranslationPermissionStatus.UNOFFICIAL });
      await db.SaveChangesAsync();

      var dto = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 1, IsUnofficial = false };
      var result = await CreateService(db).RequestPermissionAsync(dto);

      result.Status.Should().Be("PENDING");
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Xin quyền dịch - Existing Unofficial - To - Unofficial - Cập nhật To Unofficial
    /// </summary>
    public async Task RequestPermission_ExistingUnofficial_To_Unofficial_ShouldUpdateToUnofficial()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(123);
      db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = 123, IsActive = true, JoinedAt = DateTime.UtcNow });
      await SeedBaseData(db);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 57, SeriesId = 10, TeamId = 1, LanguageId = 1, Status = TranslationPermissionStatus.UNOFFICIAL, Note = "Old Note" });
      await db.SaveChangesAsync();

      var dto = new RequestPermissionDto { TeamId = 1, SeriesId = 10, LanguageId = 1, IsUnofficial = true, Note = "New Note" };
      var result = await CreateService(db).RequestPermissionAsync(dto);

      result.Status.Should().Be("UNOFFICIAL");
      result.Note.Should().Be("New Note"); // Still unofficial, just updated
    }

    // ═══════════════════════════════════════════════════════════
    // TPERMO_REV: MATRIX CÁC TRƯỜNG HỢP DUYỆT QUYỀN (10 Cases)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Duyệt quyền dịch - Ném ngoại lệ (Block) - Khi Permission không tồn tại
    /// </summary>
    public async Task ReviewPermission_ShouldThrow_WhenPermissionNotFound()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).ReviewPermissionAsync(999, new ReviewPermissionDto { IsApproved = true }));
      ex.Message.Should().Be("Permission request not found.");
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Duyệt quyền dịch - Ném ngoại lệ (Block) - Khi Caller Is Not Creator
    /// </summary>
    public async Task ReviewPermission_ShouldThrow_WhenCallerIsNotCreator()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(999); // Imposter
      await SeedBaseData(db, creatorUserId: 456);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 99, SeriesId = 10, TeamId = 1, LanguageId = 1, Status = TranslationPermissionStatus.PENDING });
      await db.SaveChangesAsync();

      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionDto { IsApproved = true }));
      ex.Message.Should().Contain("Unauthorized");
    }

    // Approve flows
    [Fact]
    /// <summary>
    /// Kiểm tra logic: Duyệt quyền dịch - Pending - Approve - Chấp thuận
    /// </summary>
    public async Task ReviewPermission_Pending_Approve_ShouldGrant()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      await SeedBaseData(db, creatorUserId: 456);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 99, SeriesId = 10, TeamId = 1, LanguageId = 1, Status = TranslationPermissionStatus.PENDING });
      await db.SaveChangesAsync();

      var result = await CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionDto { IsApproved = true });
      result.Status.Should().Be("GRANTED");
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Duyệt quyền dịch - Denied - Approve - Chấp thuận
    /// </summary>
    public async Task ReviewPermission_Denied_Approve_ShouldGrant() // Creator re-approves an earlier denied request
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      await SeedBaseData(db, creatorUserId: 456);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 99, SeriesId = 10, TeamId = 1, LanguageId = 1, Status = TranslationPermissionStatus.DENIED });
      await db.SaveChangesAsync();

      var result = await CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionDto { IsApproved = true });
      result.Status.Should().Be("GRANTED");
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Duyệt quyền dịch - Unofficial - Approve - Chấp thuận
    /// </summary>
    public async Task ReviewPermission_Unofficial_Approve_ShouldGrant() // Creator converts Unofficial -> Official
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      await SeedBaseData(db, creatorUserId: 456);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 99, SeriesId = 10, TeamId = 1, LanguageId = 1, Status = TranslationPermissionStatus.UNOFFICIAL });
      await db.SaveChangesAsync();

      var result = await CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionDto { IsApproved = true });
      result.Status.Should().Be("GRANTED");
    }

    // Reject/Revoke flows
    [Fact]
    /// <summary>
    /// Kiểm tra logic: Duyệt quyền dịch - Pending - Reject - Từ chối
    /// </summary>
    public async Task ReviewPermission_Pending_Reject_ShouldDeny()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      await SeedBaseData(db, creatorUserId: 456);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 99, SeriesId = 10, TeamId = 1, LanguageId = 1, Status = TranslationPermissionStatus.PENDING });
      await db.SaveChangesAsync();

      var result = await CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionDto { IsApproved = false });
      result.Status.Should().Be("DENIED");
      
      var p = await db.TranslationPermissions.FindAsync(99);
      p!.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Duyệt quyền dịch - Granted - Reject - Từ chối
    /// </summary>
    public async Task ReviewPermission_Granted_Reject_ShouldDeny() // Equivalent to Revoking!
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      await SeedBaseData(db, creatorUserId: 456);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 99, SeriesId = 10, TeamId = 1, LanguageId = 1, Status = TranslationPermissionStatus.GRANTED });
      await db.SaveChangesAsync();

      var result = await CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionDto { IsApproved = false });
      result.Status.Should().Be("DENIED"); 
    }

    // Translation Sync Checks
    [Fact]
    /// <summary>
    /// Kiểm tra logic: Duyệt quyền dịch - Đồng bộ Multiple Translations - To Official
    /// </summary>
    public async Task ReviewPermission_ShouldSyncMultipleTranslations_ToOfficial()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      await SeedBaseData(db, creatorUserId: 456);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 99, SeriesId = 10, TeamId = 1, LanguageId = 1, Status = TranslationPermissionStatus.UNOFFICIAL });
      
      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 1, PermissionId = 99, ChapterId = 100, IsOfficial = false });
      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 2, PermissionId = 99, ChapterId = 101, IsOfficial = false });
      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 3, PermissionId = 88, ChapterId = 100, IsOfficial = false }); // Different permission
      await db.SaveChangesAsync();

      await CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionDto { IsApproved = true });

      var t1 = await db.Translations.FindAsync(1);
      var t2 = await db.Translations.FindAsync(2);
      var t3 = await db.Translations.FindAsync(3);

      t1!.IsOfficial.Should().BeTrue();
      t2!.IsOfficial.Should().BeTrue();
      t3!.IsOfficial.Should().BeFalse(); // Untouched
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Duyệt quyền dịch - Đồng bộ Multiple Translations - To Unofficial
    /// </summary>
    public async Task ReviewPermission_ShouldSyncMultipleTranslations_ToUnofficial()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      await SeedBaseData(db, creatorUserId: 456);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 99, SeriesId = 10, TeamId = 1, LanguageId = 1, Status = TranslationPermissionStatus.GRANTED });
      
      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 1, PermissionId = 99, ChapterId = 100, IsOfficial = true });
      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 2, PermissionId = 99, ChapterId = 101, IsOfficial = true });
      await db.SaveChangesAsync();

      await CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionDto { IsApproved = false }); // Creator revokes

      var t1 = await db.Translations.FindAsync(1);
      var t2 = await db.Translations.FindAsync(2);
      t1!.IsOfficial.Should().BeFalse();
      t2!.IsOfficial.Should().BeFalse();
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Duyệt quyền dịch - Should NotNếuy Only Active Team Members
    /// </summary>
    public async Task ReviewPermission_ShouldNotifyOnlyActiveTeamMembers()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(456);
      await SeedBaseData(db, creatorUserId: 456);
      
      db.TeamMembers.Add(new TeamMember { MembershipId = 101, TeamId = 1, UserId = 111, IsActive = true, JoinedAt = DateTime.UtcNow });
      db.TeamMembers.Add(new TeamMember { MembershipId = 102, TeamId = 1, UserId = 222, IsActive = false, JoinedAt = DateTime.UtcNow }); // Inactive
      
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 99, SeriesId = 10, TeamId = 1, LanguageId = 1, Status = TranslationPermissionStatus.PENDING });
      await db.SaveChangesAsync();

      await CreateService(db).ReviewPermissionAsync(99, new ReviewPermissionDto { IsApproved = true });

      _mockNotificationService.Verify(n => n.CreateNotificationAsync(
          It.IsAny<int>(), "Yêu cầu dịch truyện được chấp thuận", It.IsAny<string>(),
          It.IsAny<string>(), NotificationType.TRANSLATION_GRANTED), Times.Exactly(1)); // Only the active member was notified
    }

    // ═══════════════════════════════════════════════════════════
    // TPERMO_GET: MATRIX CÁC TRƯỜNG HỢP QUERY (4 Cases)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Get Team Permissions - Trả về Only Requested Team
    /// </summary>
    public async Task GetTeamPermissions_ShouldReturnOnlyRequestedTeam()
    {
      var db = CreateDb();
      await SeedBaseData(db);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 1, TeamId = 1, SeriesId = 10 });
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 2, TeamId = 2, SeriesId = 10 }); // Other team
      await db.SaveChangesAsync();

      var result = await CreateService(db).GetTeamPermissionsAsync(1);
      result.Should().ContainSingle();
      result.First().PermissionId.Should().Be(1);
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Get Team Permissions - Should Order By Id Descending
    /// </summary>
    public async Task GetTeamPermissions_ShouldOrderByIdDescending()
    {
      var db = CreateDb();
      await SeedBaseData(db);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 1, TeamId = 1, SeriesId = 10 });
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 2, TeamId = 1, SeriesId = 11 }); // Distinct SeriesId
      await db.SaveChangesAsync();

      var result = await CreateService(db).GetTeamPermissionsAsync(1);
      result.First().PermissionId.Should().Be(2); 
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Get Creator Permissions - Trả về Only Requested Creator
    /// </summary>
    public async Task GetCreatorPermissions_ShouldReturnOnlyRequestedCreator()
    {
      var db = CreateDb();
      await SeedBaseData(db, creatorUserId: 456); 
      // Add a second creator
      db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 6, UserId = 789, PenName = "Author 2" });
      
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 1, GrantedBy = 456, TeamId = 1, SeriesId = 10 });
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 2, GrantedBy = 789, TeamId = 1, SeriesId = 11 }); // Distinct SeriesId
      await db.SaveChangesAsync();

      var result = await CreateService(db).GetCreatorPermissionsAsync(456);
      result.Should().ContainSingle();
      result.First().PermissionId.Should().Be(1);
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Get Creator Permissions - Trả về Empty Nếu User Id Not Creator
    /// </summary>
    public async Task GetCreatorPermissions_ShouldReturnEmptyIfUserIdNotCreator()
    {
      var db = CreateDb();
      await SeedBaseData(db, creatorUserId: 456);
      var result = await CreateService(db).GetCreatorPermissionsAsync(999); // Normal user
      result.Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════
    // TPERMO_AUTO: MATRIX CRONJOB (3 Cases)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Auto Deny Expired Requests - Should Not Deny - Nếu Under72Hours
    /// </summary>
    public async Task AutoDenyExpiredRequests_ShouldNotDeny_IfUnder72Hours()
    {
      var db = CreateDb();
      await SeedBaseData(db);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 1, TeamId = 1, SeriesId = 10, Status = TranslationPermissionStatus.PENDING, CreatedAt = DateTime.UtcNow.AddHours(-71) });
      await db.SaveChangesAsync();

      var count = await CreateService(db).AutoDenyExpiredRequestsAsync(72);
      count.Should().Be(0);
      var p = await db.TranslationPermissions.FindAsync(1);
      p!.Status.Should().Be(TranslationPermissionStatus.PENDING);
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Auto Deny Expired Requests - Từ chối - Nếu Exactly Or Over72Hours
    /// </summary>
    public async Task AutoDenyExpiredRequests_ShouldDeny_IfExactlyOrOver72Hours()
    {
      var db = CreateDb();
      await SeedBaseData(db, creatorUserId: 456);
      db.TeamMembers.Add(new TeamMember { MembershipId = 101, TeamId = 1, UserId = 111, IsActive = true });
      db.Series.Add(new Series { SeriesId = 11, CreatorId = 5, Title = "Another Manga" });
      
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 1, TeamId = 1, SeriesId = 10, Status = TranslationPermissionStatus.PENDING, CreatedAt = DateTime.UtcNow.AddHours(-73) });
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 2, TeamId = 1, SeriesId = 11, Status = TranslationPermissionStatus.PENDING, CreatedAt = DateTime.UtcNow.AddHours(-100) }); // Distinct SeriesId
      await db.SaveChangesAsync();

      var count = await CreateService(db).AutoDenyExpiredRequestsAsync(72);
      count.Should().Be(2);
      
      var p1 = await db.TranslationPermissions.FindAsync(1);
      p1!.Status.Should().Be(TranslationPermissionStatus.DENIED);
      
      _mockNotificationService.Verify(n => n.CreateNotificationAsync(
          111, "Yêu cầu dịch truyện bị từ chối tự động", It.IsAny<string>(), It.IsAny<string>(), NotificationType.TRANSLATION_REVOKED), Times.Exactly(2));
    }

    [Fact]
    /// <summary>
    /// Kiểm tra logic: Auto Deny Expired Requests - Bỏ qua Non Pending Requests - Even Nếu Old
    /// </summary>
    public async Task AutoDenyExpiredRequests_ShouldIgnoreNonPendingRequests_EvenIfOld()
    {
      var db = CreateDb();
      await SeedBaseData(db);
      db.Series.Add(new Series { SeriesId = 11, CreatorId = 5, Title = "Another Manga" });
      db.Series.Add(new Series { SeriesId = 12, CreatorId = 5, Title = "Third Manga" });
      
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 1, TeamId = 1, SeriesId = 10, Status = TranslationPermissionStatus.GRANTED, CreatedAt = DateTime.UtcNow.AddHours(-100) });
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 2, TeamId = 1, SeriesId = 11, Status = TranslationPermissionStatus.UNOFFICIAL, CreatedAt = DateTime.UtcNow.AddHours(-100) });
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 3, TeamId = 1, SeriesId = 12, Status = TranslationPermissionStatus.DENIED, CreatedAt = DateTime.UtcNow.AddHours(-100) });
      await db.SaveChangesAsync();

      var count = await CreateService(db).AutoDenyExpiredRequestsAsync(72);
      count.Should().Be(0); // 0 because none are PENDING
    }
  }
}

