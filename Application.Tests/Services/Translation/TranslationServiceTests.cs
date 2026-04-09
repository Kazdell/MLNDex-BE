using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Chapter;
using Application.DTOs.Translation.Requests;
using Application.DTOs.Translation.Responses;
using Application.Interfaces.AIModeration;
using Application.Interfaces.Common;
using Application.Interfaces.Creator;
using Application.Interfaces.Notification;
using Application.Services.Translation;
using Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

using Application.Tests.Shared;
using Infrastructure.Data;

namespace Application.Tests.Services.Translation
{
  [Collection("Database collection")]
  public class TranslationServiceTests : IAsyncLifetime
  {
    private readonly Mock<IUserContext> _mockUserContext;
    private readonly Mock<IStorageService> _mockStorage;
    private readonly Mock<ILogger<TranslationService>> _mockLogger;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<IModerationService> _mockModerationService;
    private readonly DatabaseFixture _fixture;

    public TranslationServiceTests(DatabaseFixture fixture)
    {
      _fixture = fixture;
      _mockUserContext = new Mock<IUserContext>();
      _mockStorage = new Mock<IStorageService>();
      _mockLogger = new Mock<ILogger<TranslationService>>();
      _mockNotificationService = new Mock<INotificationService>();
      _mockModerationService = new Mock<IModerationService>();
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
          new Domain.Entities.User { UserId = 2, Username = "user2", Email = "u2@test.com", DisplayName = "D2", PasswordHash = "X" }
      );
      await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;

    private MlndexDbContext CreateDb() => _fixture.CreateDbContext();

    private TranslationService CreateService(MlndexDbContext db)
      => new TranslationService(
          db,
          _mockUserContext.Object,
          _mockStorage.Object,
          _mockLogger.Object,
          _mockNotificationService.Object,
          _mockModerationService.Object);

    /// <summary>
    /// Seeds base data and returns the auto-generated teamId.
    /// TranslationTeam and TeamMember use IDENTITY (auto-gen IDs).
    /// </summary>
    private async Task<int> SeedBaseData(MlndexDbContext db)
    {
      // Phase 1: Independent entities (manual IDs)
      db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 5, UserId = 456, PenName = "Author" });
      db.Languages.Add(new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" });
      db.Languages.Add(new Language { LanguageId = 2, Name = "English", Code = "en" });
      await db.SaveChangesAsync();

      // Phase 2: Series + Chapters (manual IDs)
      db.Series.Add(new Series { SeriesId = 10, CreatorId = 5, Title = "Epic Manga" });
      await db.SaveChangesAsync();
      db.Chapters.Add(new Chapter { ChapterId = 100, SeriesId = 10, ChapterNumber = 1 });
      await db.SaveChangesAsync();

      // Phase 3: Teams + Members (auto-gen IDs — IDENTITY enabled)
      var team = new TranslationTeam { TeamName = "Hero Team", Slug = "hero-team", LeaderId = 1, LanguageId = 1 };
      db.TranslationTeams.Add(team);
      await db.SaveChangesAsync();
      db.TeamMembers.Add(new TeamMember { TeamId = team.TeamId, UserId = 111, IsActive = true, JoinedAt = DateTime.UtcNow });
      await db.SaveChangesAsync();
      return team.TeamId;
    }

    // ═══════════════════════════════════════════════════════════
    // T-TRANS-UP: UPLOAD TRANSLATION
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Upload_Official_ShouldThrow_WhenPermissionNotFound()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(111);
      var teamId = await SeedBaseData(db);

      var dto = new UploadTranslationRequest { PermissionId = 999, ChapterId = 100, LanguageId = 1 };
      var ex = await Assert.ThrowsAsync<Application.Exceptions.AppException>(() => CreateService(db).UploadTranslationAsync(dto));
      ex.Message.Should().Be("Translation permission record not found.");
    }

    [Fact]
    public async Task Upload_Official_ShouldThrow_WhenLanguageMismatch()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(111);
      var teamId = await SeedBaseData(db);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 1, TeamId = teamId, SeriesId = 10, LanguageId = 1, GrantedBy = 456 });
      await db.SaveChangesAsync();

      var dto = new UploadTranslationRequest { PermissionId = 1, ChapterId = 100, LanguageId = 2 }; // Wrong language
      var ex = await Assert.ThrowsAsync<Application.Exceptions.AppException>(() => CreateService(db).UploadTranslationAsync(dto));
      ex.Message.Should().Contain("Language mismatch");
    }

    [Fact]
    public async Task Upload_Official_ShouldThrow_WhenSeriesMismatch()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(111);
      var teamId = await SeedBaseData(db);
      db.Series.Add(new Series { SeriesId = 99, CreatorId = 5, Title = "Other Manga" });
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 1, TeamId = teamId, SeriesId = 99, LanguageId = 1, GrantedBy = 456 });
      await db.SaveChangesAsync();

      var dto = new UploadTranslationRequest { PermissionId = 1, ChapterId = 100, LanguageId = 1 };
      var ex = await Assert.ThrowsAsync<Application.Exceptions.AppException>(() => CreateService(db).UploadTranslationAsync(dto));
      ex.Message.Should().Be("Permission not valid for this series.");
    }

    [Fact]
    public async Task Upload_Official_ShouldThrow_WhenUserNotActiveMember()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(999); // Imposter
      var teamId = await SeedBaseData(db);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 1, TeamId = teamId, SeriesId = 10, LanguageId = 1, GrantedBy = 456 });
      await db.SaveChangesAsync();

      var dto = new UploadTranslationRequest { PermissionId = 1, ChapterId = 100, LanguageId = 1 };
      var ex = await Assert.ThrowsAsync<Application.Exceptions.AppException>(() => CreateService(db).UploadTranslationAsync(dto));
      ex.Message.Should().Be("Uploader is not an active member or leader of the translation team.");
    }

    [Fact]
    public async Task Upload_Official_ShouldUploadText_AndNotify()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(111);
      var teamId = await SeedBaseData(db);
      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 1, TeamId = teamId, SeriesId = 10, LanguageId = 1, Status = TranslationPermissionStatus.GRANTED, GrantedBy = 456 });
      await db.SaveChangesAsync();

      _mockStorage.Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync("https://cdn.mlndex.com/test-text.txt");

      var dto = new UploadTranslationRequest
      {
        PermissionId = 1,
        ChapterId = 100,
        LanguageId = 1,
        ContentType = ContentType.TEXT,
        ContentText = "Hello World! This is a test."
      };

      var result = await CreateService(db).UploadTranslationAsync(dto);

      result.Should().NotBeNull();
      result.IsOfficial.Should().BeTrue();
      result.TextContent.Should().Be("https://cdn.mlndex.com/test-text.txt");

      _mockStorage.Verify(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once());
      _mockModerationService.Verify(m => m.EnqueueTranslationForModerationAsync(result.TranslationId, It.IsAny<CancellationToken>()), Times.Once());
      _mockNotificationService.Verify(n => n.CreateNotificationAsync(111, "Đã đưa vào hàng đợi kiểm duyệt!", It.IsAny<string>(), It.IsAny<string>(), NotificationType.SYSTEM), Times.Once());

      var entity = await db.Translations.Include(t => t.TranslationText).FirstOrDefaultAsync(t => t.TranslationId == result.TranslationId);
      entity!.TranslationText!.WordCount.Should().Be(6);
    }

    [Fact]
    public async Task Upload_Unofficial_ShouldAutoCreatePermission_AndUploadImages()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(111);
      var teamId = await SeedBaseData(db);

      _mockStorage.Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync("https://cdn.mlndex.com/img1.png");

      var dto = new UploadTranslationRequest
      {
        TeamId = teamId,
        ChapterId = 100,
        LanguageId = 1,
        ContentType = ContentType.IMAGE,
        Pages = new List<UploadPageDto> { new UploadPageDto { FileName = "p1.png", FileStream = new MemoryStream() } }
      };

      var result = await CreateService(db).UploadTranslationAsync(dto);

      result.IsOfficial.Should().BeFalse();
      result.Pages.Should().ContainSingle().Which.Should().Be("https://cdn.mlndex.com/img1.png");

      var transEntity = await db.Translations.FindAsync(result.TranslationId);
      transEntity!.PermissionId.Should().NotBeNull();

      var newPerm = await db.TranslationPermissions.FindAsync(transEntity.PermissionId);
      newPerm.Should().NotBeNull();
      newPerm!.Status.Should().Be(TranslationPermissionStatus.UNOFFICIAL);
      newPerm.TeamId.Should().Be(teamId);
    }

    [Fact]
    public async Task Upload_Unofficial_ShouldThrow_WhenChapterIsLocked()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(111);
      var teamId = await SeedBaseData(db);

      var chapter = await db.Chapters.FindAsync(100);
      chapter!.LockStatus = ChapterLockStatus.LOCKED;
      chapter.UnlockTime = DateTime.UtcNow.AddDays(1);
      await db.SaveChangesAsync();

      var dto = new UploadTranslationRequest
      {
        TeamId = teamId,
        ChapterId = 100,
        LanguageId = 1,
        ContentType = ContentType.TEXT,
        ContentText = "Attempt to translate locked chapter"
      };

      var ex = await Assert.ThrowsAsync<Application.Exceptions.AppException>(() => CreateService(db).UploadTranslationAsync(dto));
      ex.Message.Should().Contain("không được phép dịch các chương đang khóa");
    }

    [Fact]
    public async Task Upload_ShouldRollbackAndThrow_WhenStorageUploadFails()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(111);
      var teamId = await SeedBaseData(db);

      _mockStorage.Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new Exception("Upload API Error"));

      var dto = new UploadTranslationRequest
      {
        TeamId = teamId,
        ChapterId = 100,
        LanguageId = 1,
        ContentType = ContentType.TEXT,
        ContentText = "Will fail"
      };

      await Assert.ThrowsAsync<Exception>(() => CreateService(db).UploadTranslationAsync(dto));

      var count = await db.Translations.CountAsync();
      count.Should().Be(0);
    }

    [Fact]
    public async Task Upload_Unofficial_ShouldThrow_WhenDuplicateLanguageForTeam()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(111);
      var teamId = await SeedBaseData(db);

      // Add existing translation
      db.Translations.Add(new Domain.Entities.Translation
      {
        TranslationId = 999,
        TeamId = teamId,
        ChapterId = 100,
        LanguageId = 1
      });
      await db.SaveChangesAsync();

      var dto = new UploadTranslationRequest
      {
        TeamId = teamId,
        ChapterId = 100,
        LanguageId = 1,
        ContentType = ContentType.TEXT,
        ContentText = "Hi"
      };

      var ex = await Assert.ThrowsAsync<Application.Exceptions.AppException>(() => CreateService(db).UploadTranslationAsync(dto));
      ex.Message.Should().Contain("Nhóm dịch đã đăng một bản dịch ngôn ngữ này cho chương gốc");
    }

    // ═══════════════════════════════════════════════════════════
    // T-TRANS-EDIT / DEL: EDIT AND DELETE TRANSLATION
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task EditTranslation_ShouldThrow_WhenCallerNotActiveMember()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(999); // Imposter
      var teamId = await SeedBaseData(db);

      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 5, TeamId = teamId, SeriesId = 10, LanguageId = 1, GrantedBy = 456 });
      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 55, PermissionId = 5, ChapterId = 100 });
      await db.SaveChangesAsync();

      var ex = await Assert.ThrowsAsync<Application.Exceptions.AppException>(() => CreateService(db).EditTranslationAsync(55, new EditTranslationRequest { LanguageId = 2 }));
      ex.Message.Should().Be("Unauthorized to edit.");
    }

    [Fact]
    public async Task EditTranslation_ShouldUpdateLanguage_WhenCallerIsMember()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(111); // Member
      var teamId = await SeedBaseData(db);

      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 5, TeamId = teamId, SeriesId = 10, LanguageId = 1, GrantedBy = 456 });
      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 55, PermissionId = 5, ChapterId = 100, LanguageId = 1 });
      await db.SaveChangesAsync();

      var result = await CreateService(db).EditTranslationAsync(55, new EditTranslationRequest { LanguageId = 2 });
      result.LanguageId.Should().Be(2);

      var trans = await db.Translations.FindAsync(55);
      trans!.LanguageId.Should().Be(2);
    }

    [Fact]
    public async Task DeleteTranslation_ShouldReturnTrue_AndRemoveFromDb()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(111); // Member
      var teamId = await SeedBaseData(db);

      db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 5, TeamId = teamId, SeriesId = 10, LanguageId = 1, GrantedBy = 456 });
      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 55, PermissionId = 5, ChapterId = 100 });
      await db.SaveChangesAsync();

      var success = await CreateService(db).DeleteTranslationAsync(55);
      success.Should().BeTrue();

      var count = await db.Translations.CountAsync();
      count.Should().Be(0);
    }

    [Fact]
    public async Task DeleteTranslation_ShouldThrow_WhenDataConsistencyMissing()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(111); // Member
      var teamId = await SeedBaseData(db);

      // Simulating the bug (null permission)
      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 55, PermissionId = null, ChapterId = 100 });
      await db.SaveChangesAsync();

      var ex = await Assert.ThrowsAsync<Application.Exceptions.AppException>(() => CreateService(db).DeleteTranslationAsync(55));
      ex.Message.Should().Be("Data consistency error: Missing TranslationPermission.");
    }

    // ═══════════════════════════════════════════════════════════
    // T-TRANS-GET: QUERIES
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetTranslationsBySeries_ShouldReturnMappedDtos()
    {
      var db = CreateDb();
      var teamId = await SeedBaseData(db);

      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 1, ChapterId = 100, LanguageId = 1, IsOfficial = true });
      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 2, ChapterId = 100, LanguageId = 2, IsOfficial = false });
      await db.SaveChangesAsync();

      var result = await CreateService(db).GetTranslationsBySeriesAsync(10);
      result.Count().Should().Be(2);
      result.Any(r => r.IsOfficial).Should().BeTrue();
    }
  }
}
