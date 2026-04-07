using System;
using System.Threading.Tasks;
using Application.Services.ReportSystem;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.ReportSystem;

public class TrustScoreService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public TrustScoreService_CsvAlignedTests(ITestOutputHelper output)
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

  private static async Task SeedAsync(MlndexDbContext db)
  {
    db.Users.Add(new User { UserId = 1, Username = "user1", Email = "user1@test.com", DisplayName = "User 1", PasswordHash = "hash" });
    db.Appeals.Add(new Appeal { AppealId = 1, UserId = 1, Reason = "please review", Status = AppealStatus.Pending, CreatedAt = DateTime.UtcNow.AddHours(-2) });

    db.TranslationTeams.Add(new TranslationTeam { TeamId = 8, TeamName = "Team H", Slug = "team-h" });
    db.Series.Add(new Series { SeriesId = 100, Title = "Series A" });
    db.Chapters.Add(new Chapter { ChapterId = 200, SeriesId = 100, ChapterNumber = 1, Title = "Chapter A", ContentType = ContentType.IMAGE, Status = ChapterStatus.DRAFT, ModerationStatus = ModerationStatus.PENDING, LockStatus = ChapterLockStatus.FREE, LanguageId = 1 });
    db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 12, TeamId = 8, SeriesId = 100, LanguageId = 1, Status = TranslationPermissionStatus.GRANTED, Origin = PermissionOrigin.REQUESTED_BY_TEAM });
    db.Translations.Add(new Domain.Entities.Translation { TranslationId = 300, ChapterId = 200, PermissionId = 12, LanguageId = 1, ContentType = ContentType.IMAGE, PublishedAt = DateTime.UtcNow.AddDays(-1) });
    db.TranslationCredits.Add(new TranslationCredit { TranslationId = 300, UserId = 1, Role = TranslationRole.TRANSLATOR });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task GetPendingAppealsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = new TrustScoreService(db);

    var output = await service.GetPendingAppealsAsync();

    _output.WriteLine("Input: one pending appeal");
    _output.WriteLine($"Output: count={output.Count}, firstAppealId={output[0].AppealId}");

    output.Should().HaveCount(1);
    output[0].AppealId.Should().Be(1);
  }

  [Fact]
  public async Task GetPendingAppealsAsync_TC02_Empty_WhenNoPendingAppeal()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var appeal = await db.Appeals.FirstAsync(a => a.AppealId == 1);
    appeal.Status = AppealStatus.Approved;
    await db.SaveChangesAsync();

    var service = new TrustScoreService(db);
    var output = await service.GetPendingAppealsAsync();

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetPendingAppealsAsync_TC03_BusinessRule_PaginatesResults()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    db.Appeals.Add(new Appeal { AppealId = 2, UserId = 1, Reason = "appeal 2", Status = AppealStatus.Pending, CreatedAt = DateTime.UtcNow.AddHours(-1) });
    await db.SaveChangesAsync();

    var service = new TrustScoreService(db);
    var output = await service.GetPendingAppealsAsync(page: 1, limit: 1);

    output.Should().HaveCount(1);
  }

  [Fact]
  public async Task GetUserTranslationHistoryAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = new TrustScoreService(db);

    var output = await service.GetUserTranslationHistoryAsync(1);

    _output.WriteLine("Input: userId=1 with one translation credit");
    _output.WriteLine($"Output: count={output.Count}, firstTranslation={output[0].TranslationId}");

    output.Should().HaveCount(1);
    output[0].TranslationId.Should().Be(300);
  }

  [Fact]
  public async Task GetUserTranslationHistoryAsync_TC02_Empty_WhenUserHasNoCredits()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = new TrustScoreService(db);

    var output = await service.GetUserTranslationHistoryAsync(999);

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetUserTranslationHistoryAsync_TC03_BusinessRule_OrderedByPublishedDateDesc()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);

    db.Translations.Add(new Domain.Entities.Translation
    {
      TranslationId = 301,
      ChapterId = 200,
      PermissionId = 12,
      LanguageId = 1,
      ContentType = ContentType.IMAGE,
      PublishedAt = DateTime.UtcNow
    });
    db.TranslationCredits.Add(new TranslationCredit { TranslationId = 301, UserId = 1, Role = TranslationRole.TRANSLATOR });
    await db.SaveChangesAsync();

    var service = new TrustScoreService(db);
    var output = await service.GetUserTranslationHistoryAsync(1);

    output.Should().HaveCount(2);
    output[0].TranslationId.Should().Be(301);
  }
}
