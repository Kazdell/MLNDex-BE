using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Translation;
using Application.Interfaces.AIModeration;
using Application.Interfaces.Common;
using Application.Interfaces.Creator;
using Application.Interfaces.Notification;
using Application.Services.Translation;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.Translation;

public class TranslationService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;
  private readonly Mock<IUserContext> _mockUserContext = new();
  private readonly Mock<IStorageService> _mockStorage = new();
  private readonly Mock<ILogger<TranslationService>> _mockLogger = new();
  private readonly Mock<INotificationService> _mockNotificationService = new();
  private readonly Mock<IModerationService> _mockModerationService = new();

  public TranslationService_CsvAlignedTests(ITestOutputHelper output)
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

  private TranslationService CreateService(MlndexDbContext db)
  {
    _mockStorage
      .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync("https://cdn.test/content.txt");

    _mockStorage
      .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    _mockModerationService
      .Setup(x => x.EnqueueTranslationForModerationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    _mockNotificationService
      .Setup(x => x.CreateNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()))
      .ReturnsAsync(new Application.DTOs.Notification.NotificationDto
      {
        NotificationId = 1,
        Title = "ok",
        Message = "ok"
      });

    return new TranslationService(
      db,
      _mockUserContext.Object,
      _mockStorage.Object,
      _mockLogger.Object,
      _mockNotificationService.Object,
      _mockModerationService.Object);
  }

  private static async Task SeedUploadBaseAsync(MlndexDbContext db, int userId = 10)
  {
    db.Users.Add(new User { UserId = userId, Username = "uploader", DisplayName = "Uploader", Email = "uploader@test.com", PasswordHash = "hash" });
    db.Languages.Add(new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" });
    db.Series.Add(new Series { SeriesId = 1, CreatorId = 1, Title = "Series A" });
    db.Chapters.Add(new Chapter { ChapterId = 1, SeriesId = 1, ChapterNumber = 1, Title = "Chapter 1" });
    db.TranslationTeams.Add(new TranslationTeam { TeamId = 1, LeaderId = userId, TeamName = "Team A", Slug = "team-a" });
    db.TeamMembers.Add(new TeamMember { TeamId = 1, UserId = userId, Role = TeamMemberRole.LEADER, JoinedAt = DateTime.UtcNow, IsActive = true });
    db.TranslationPermissions.Add(new TranslationPermission
    {
      PermissionId = 100,
      TeamId = 1,
      SeriesId = 1,
      LanguageId = 1,
      Status = TranslationPermissionStatus.GRANTED,
      Origin = PermissionOrigin.REQUESTED_BY_TEAM
    });
    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task UploadTranslationAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUploadBaseAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(10);

    var service = CreateService(db);
    var input = new UploadTranslationDto
    {
      ChapterId = 1,
      PermissionId = 100,
      LanguageId = 1,
      ContentType = ContentType.TEXT,
      ContentText = "Hello translated content"
    };

    var output = await service.UploadTranslationAsync(input);

    _output.WriteLine($"Input: ChapterId={input.ChapterId}, PermissionId={input.PermissionId}, ContentType={input.ContentType}");
    _output.WriteLine($"Output: TranslationId={output.TranslationId}, ModerationStatus={output.ModerationStatus}");

    output.TranslationId.Should().BeGreaterThan(0);
    output.ContentType.Should().Be(ContentType.TEXT.ToString());
    _mockModerationService.Verify(x => x.EnqueueTranslationForModerationAsync(output.TranslationId, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task UploadTranslationAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUploadBaseAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(null as int?);

    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UploadTranslationAsync(new UploadTranslationDto
    {
      ChapterId = 1,
      PermissionId = 100,
      LanguageId = 1,
      ContentType = ContentType.TEXT,
      ContentText = "x"
    }));

    _output.WriteLine("Input: UserId=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task UploadTranslationAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUploadBaseAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(10);

    var service = CreateService(db);

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.UploadTranslationAsync(null!));

    _output.WriteLine("Input: dto=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task UploadTranslationAsync_TC04_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUploadBaseAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(10);

    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<Exception>(() => service.UploadTranslationAsync(new UploadTranslationDto
    {
      ChapterId = 1,
      PermissionId = 999,
      LanguageId = 1,
      ContentType = ContentType.TEXT,
      ContentText = "x"
    }));

    _output.WriteLine("Input: PermissionId=999");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Be("Translation permission record not found.");
  }

  [Fact]
  public async Task UploadTranslationAsync_TC05_BusinessRule()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUploadBaseAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(10);

    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<Exception>(() => service.UploadTranslationAsync(new UploadTranslationDto
    {
      ChapterId = 1,
      PermissionId = 100,
      LanguageId = 2,
      ContentType = ContentType.TEXT,
      ContentText = "x"
    }));

    _output.WriteLine("Input: language mismatch");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Be("Language mismatch with the requested permission language.");
  }

  [Fact]
  public async Task GetTranslationByIdAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.Translations.Add(new Domain.Entities.Translation { TranslationId = 1, ChapterId = 1, PermissionId = 1, LanguageId = 1, ContentType = ContentType.TEXT });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetTranslationByIdAsync(1);

    _output.WriteLine("Input: translationId=1");
    _output.WriteLine($"Output: {(output == null ? "null" : output.TranslationId.ToString())}");

    output.Should().NotBeNull();
    output!.TranslationId.Should().Be(1);
  }

  [Fact]
  public async Task GetTranslationByIdAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = CreateService(db);

    var output = await service.GetTranslationByIdAsync(-1);

    _output.WriteLine("Input: translationId=-1");
    _output.WriteLine($"Output: {(output == null ? "null" : output.TranslationId.ToString())}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task GetTranslationByIdAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetTranslationByIdAsync(1));

    _output.WriteLine("Input: disposed context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetAllTranslationsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.Translations.Add(new Domain.Entities.Translation { TranslationId = 1, ChapterId = 1, PermissionId = 1, LanguageId = 1, ContentType = ContentType.TEXT });
    db.Translations.Add(new Domain.Entities.Translation { TranslationId = 2, ChapterId = 2, PermissionId = 1, LanguageId = 1, ContentType = ContentType.IMAGE });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = (await service.GetAllTranslationsAsync()).ToList();

    _output.WriteLine("Input: query all translations");
    _output.WriteLine($"Output count={output.Count}");

    output.Count.Should().Be(2);
  }

  [Fact]
  public async Task GetAllTranslationsAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = CreateService(db);

    var output = (await service.GetAllTranslationsAsync()).ToList();

    _output.WriteLine("Input: empty dataset");
    _output.WriteLine($"Output count={output.Count}");

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetAllTranslationsAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(async () => _ = (await service.GetAllTranslationsAsync()).ToList());

    _output.WriteLine("Input: disposed context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task EditTranslationAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUploadBaseAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(10);

    db.Translations.Add(new Domain.Entities.Translation { TranslationId = 10, ChapterId = 1, PermissionId = 100, LanguageId = 1, ContentType = ContentType.TEXT });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.EditTranslationAsync(10, new EditTranslationDto { LanguageId = 2 });

    _output.WriteLine("Input: edit translation language 1->2");
    _output.WriteLine($"Output languageId={output.LanguageId}");

    output.LanguageId.Should().Be(2);
  }

  [Fact]
  public async Task EditTranslationAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUploadBaseAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(null as int?);

    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EditTranslationAsync(1, new EditTranslationDto { LanguageId = 2 }));

    _output.WriteLine("Input: userId=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task EditTranslationAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUploadBaseAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(10);

    db.Translations.Add(new Domain.Entities.Translation { TranslationId = 11, ChapterId = 1, PermissionId = 100, LanguageId = 1, ContentType = ContentType.TEXT });
    await db.SaveChangesAsync();

    var service = CreateService(db);

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.EditTranslationAsync(11, null!));

    _output.WriteLine("Input: dto=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task DeleteTranslationAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUploadBaseAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(10);

    db.Translations.Add(new Domain.Entities.Translation { TranslationId = 20, ChapterId = 1, PermissionId = 100, LanguageId = 1, ContentType = ContentType.TEXT });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.DeleteTranslationAsync(20);

    _output.WriteLine("Input: delete translationId=20");
    _output.WriteLine($"Output: {output}");

    output.Should().BeTrue();
    (await db.Translations.AnyAsync(t => t.TranslationId == 20)).Should().BeFalse();
  }

  [Fact]
  public async Task DeleteTranslationAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUploadBaseAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(null as int?);

    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteTranslationAsync(1));

    _output.WriteLine("Input: userId=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task DeleteTranslationAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUploadBaseAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Throws(new Exception("User context failure"));

    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<Exception>(() => service.DeleteTranslationAsync(1));

    _output.WriteLine("Input: user context throws");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Be("User context failure");
  }

  [Fact]
  public async Task DeleteTranslationAsync_TC04_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUploadBaseAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(10);

    var service = CreateService(db);
    var output = await service.DeleteTranslationAsync(404);

    _output.WriteLine("Input: translationId=404");
    _output.WriteLine($"Output: {output}");

    output.Should().BeFalse();
  }

  [Fact]
  public async Task DeleteTranslationAsync_TC05_BusinessRule()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUploadBaseAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(99);

    db.Translations.Add(new Domain.Entities.Translation { TranslationId = 21, ChapterId = 1, PermissionId = 100, LanguageId = 1, ContentType = ContentType.TEXT });
    await db.SaveChangesAsync();

    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<Exception>(() => service.DeleteTranslationAsync(21));

    _output.WriteLine("Input: unauthorized delete");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Be("Unauthorized to delete.");
  }
}
