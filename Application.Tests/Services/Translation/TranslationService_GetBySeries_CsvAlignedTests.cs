using System;
using System.Linq;
using System.Threading.Tasks;
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

public class TranslationService_GetBySeries_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public TranslationService_GetBySeries_CsvAlignedTests(ITestOutputHelper output)
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

  private static TranslationService CreateService(MlndexDbContext db)
  {
    return new TranslationService(
      db,
      new Mock<IUserContext>().Object,
      new Mock<IStorageService>().Object,
      new Mock<ILogger<TranslationService>>().Object,
      new Mock<INotificationService>().Object,
      new Mock<IModerationService>().Object);
  }

  [Fact]
  public async Task GetTranslationsBySeriesAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.Chapters.Add(new Chapter { ChapterId = 1, SeriesId = 1, ChapterNumber = 1, Title = "C1" });
    db.Translations.Add(new Domain.Entities.Translation { TranslationId = 1, ChapterId = 1, PermissionId = 1, LanguageId = 1, ContentType = ContentType.TEXT });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = (await service.GetTranslationsBySeriesAsync(1)).ToList();

    _output.WriteLine("Input: seriesId=1");
    _output.WriteLine($"Output count={output.Count}");

    output.Count.Should().Be(1);
  }

  [Fact]
  public async Task GetTranslationsBySeriesAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = CreateService(db);

    var output = (await service.GetTranslationsBySeriesAsync(-1)).ToList();

    _output.WriteLine("Input: seriesId=-1");
    _output.WriteLine($"Output count={output.Count}");

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetTranslationsBySeriesAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(async () => _ = (await service.GetTranslationsBySeriesAsync(1)).ToList());

    _output.WriteLine("Input: disposed context");
    _output.WriteLine($"Output Exception Type={ex.GetType().Name}");
    ex.Should().NotBeNull();
  }
}
