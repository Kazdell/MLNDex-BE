using System;
using System.Threading.Tasks;
using Application.DTOs.ReportSystem;
using Application.Services.ReportSystem;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.ReportSystem;

public class PlagiarismReportService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public PlagiarismReportService_CsvAlignedTests(ITestOutputHelper output)
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
    db.Users.Add(new User { UserId = 1, Username = "reporter", Email = "reporter@test.com", DisplayName = "Reporter", PasswordHash = "hash" });
    db.TranslationTeams.Add(new TranslationTeam { TeamId = 7, TeamName = "Team A", Slug = "team-a" });
    db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 11, TeamId = 7, SeriesId = 100, LanguageId = 1, Status = TranslationPermissionStatus.GRANTED, Origin = PermissionOrigin.REQUESTED_BY_TEAM });
    db.Series.Add(new Series { SeriesId = 100, Title = "Series A" });
    db.Chapters.Add(new Chapter { ChapterId = 200, SeriesId = 100, Title = "Chapter 1", ChapterNumber = 1, ContentType = ContentType.IMAGE, Status = ChapterStatus.DRAFT, ModerationStatus = ModerationStatus.PENDING, LockStatus = ChapterLockStatus.FREE, LanguageId = 1 });

    db.Translations.AddRange(
      new Domain.Entities.Translation { TranslationId = 300, ChapterId = 200, PermissionId = 11, LanguageId = 1, ContentType = ContentType.IMAGE },
      new Domain.Entities.Translation { TranslationId = 301, ChapterId = 200, PermissionId = 11, LanguageId = 1, ContentType = ContentType.IMAGE });

    db.TranslationPages.AddRange(
      new TranslationPage { TransPageId = 1, TranslationId = 300, PageNumber = 1, TranslationImageUrl = "https://img/300-1.jpg" },
      new TranslationPage { TransPageId = 2, TranslationId = 301, PageNumber = 1, TranslationImageUrl = "https://img/301-1.jpg" });

    db.Reports.Add(new Report
    {
      ReportId = 1,
      ReporterId = 1,
      ContentType = ReportTargetType.ChapterTranslation,
      ContentId = 300,
      Reason = ReportReason.Plagiarism,
      Description = "suspected copy",
      Status = ReportStatus.Pending,
      CreatedAt = DateTime.UtcNow
    });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task GetPendingReportsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = new PlagiarismReportService(db);

    var output = await service.GetPendingReportsAsync();

    _output.WriteLine("Input: one pending report");
    _output.WriteLine($"Output: count={output.Count}, firstReportId={output[0].ReportId}");

    output.Should().HaveCount(1);
    output[0].ReportId.Should().Be(1);
  }

  [Fact]
  public async Task GetPendingReportsAsync_TC02_Empty_WhenAllReportsResolved()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var report = await db.Reports.FirstAsync(r => r.ReportId == 1);
    report.Status = ReportStatus.Resolved;
    await db.SaveChangesAsync();

    var service = new PlagiarismReportService(db);
    var output = await service.GetPendingReportsAsync();

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetPendingReportsAsync_TC03_BusinessRule_RespectsPagination()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    db.Reports.Add(new Report
    {
      ReportId = 2,
      ReporterId = 1,
      ContentType = ReportTargetType.ChapterTranslation,
      ContentId = 301,
      Reason = ReportReason.Plagiarism,
      Description = "suspected copy 2",
      Status = ReportStatus.Pending,
      CreatedAt = DateTime.UtcNow.AddMinutes(1)
    });
    await db.SaveChangesAsync();

    var service = new PlagiarismReportService(db);
    var output = await service.GetPendingReportsAsync(page: 1, limit: 1);

    output.Should().HaveCount(1);
  }

  [Fact]
  public async Task GetCompareDataAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = new PlagiarismReportService(db);

    var output = await service.GetCompareDataAsync(1, 301);

    _output.WriteLine("Input: reportId=1, referenceTranslationId=301");
    _output.WriteLine($"Output: reported={output.Reported.TranslationId}, reference={output.Reference.TranslationId}");

    output.Reported.TranslationId.Should().Be(300);
    output.Reference.TranslationId.Should().Be(301);
  }

  [Fact]
  public async Task GetCompareDataAsync_TC02_NotFound_WhenReportMissing()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = new PlagiarismReportService(db);

    var act = () => service.GetCompareDataAsync(9999, 301);

    await act.Should().ThrowAsync<KeyNotFoundException>();
  }

  [Fact]
  public async Task GetCompareDataAsync_TC03_NotFound_WhenReferenceTranslationMissing()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = new PlagiarismReportService(db);

    var act = () => service.GetCompareDataAsync(1, 9999);

    await act.Should().ThrowAsync<KeyNotFoundException>();
  }
}
