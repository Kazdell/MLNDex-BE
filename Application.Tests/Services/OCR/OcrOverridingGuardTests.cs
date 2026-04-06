using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.User;
using Application.Interfaces.Common;
using Application.Interfaces.Moderation;
using Application.Interfaces.Translation;
using Application.Services.Translation;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.Protected;
using Xunit;
using Application.Tests.Shared;

namespace Application.Tests.Services.OCR
{
  [Collection("Database collection")]
  public class OcrOverridingGuardTests : IAsyncLifetime
  {
    private readonly DatabaseFixture _fixture;

    public OcrOverridingGuardTests(DatabaseFixture fixture)
    {
      _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
      await _fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SaveTextLayersAsync_ShouldNotOverwrite_WhenLayerIsUserAdjusted()
    {
      // Arrange
      using var db = _fixture.CreateDbContext();
      
      var userContext = new Mock<IUserContext>();
      userContext.Setup(x => x.UserId).Returns(1);

      var ocrServiceMock = new Mock<IOCRService>();
      var aiClientMock = new Mock<IAiTranslationClient>();
      var googleClientMock = new Mock<IGoogleTranslationClient>();
      var loggerMock = new Mock<ILogger<ReaderTranslationService>>();
      
      var handlerMock = new Mock<HttpMessageHandler>();
      handlerMock.Protected()
          .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<System.Threading.CancellationToken>()
          )
          .ReturnsAsync(new HttpResponseMessage
          {
              StatusCode = System.Net.HttpStatusCode.OK,
              Content = new ByteArrayContent(new byte[] { 0x00, 0x01 }) // fake image bytes
          });

      var httpClient = new HttpClient(handlerMock.Object);

      var service = new ReaderTranslationService(
        db,
        new List<IOCRService> { ocrServiceMock.Object },
        aiClientMock.Object,
        googleClientMock.Object,
        loggerMock.Object,
        httpClient);

      // Seed data
      var user = new User { Username = "test", Email = "t@test.com", DisplayName = "T", PasswordHash = "X" };
      db.Users.Add(user);
      await db.SaveChangesAsync();

      var creatorProfile = new CreatorProfile { UserId = user.UserId, PenName = "CP" };
      db.CreatorProfiles.Add(creatorProfile);
      await db.SaveChangesAsync();

      var series = new Series { CreatorId = creatorProfile.CreatorId, Title = "Test Series", Description = "Test" };
      db.Series.Add(series);
      await db.SaveChangesAsync();

      var chapter = new Chapter { SeriesId = series.SeriesId, ChapterNumber = 1, Title = "Ch1" };
      db.Chapters.Add(chapter);
      await db.SaveChangesAsync();

      var page = new ChapterPage { ChapterId = chapter.ChapterId, PageNumber = 1, ImageUrl = "http://test" };
      db.ChapterPages.Add(page);
      await db.SaveChangesAsync();
      
      // Seed existing text layer WITH IsUserAdjusted = true
      var existingLayer = new PageTextLayer 
      { 
        PageId = page.PageId, 
        X = 10, Y = 10, Width = 100, Height = 50, 
        OriginalText = "Old Text", 
        TranslatedText = "Dịch Cũ",
        IsUserAdjusted = true,
        SourceLanguage = "ja",
        TargetLanguage = "vi",
        TranslationProvider = "Google"
      };
      db.PageTextLayers.Add(existingLayer);
      await db.SaveChangesAsync();

      // Act: Simulate an automated OCR re-scan submitting new bounding boxes
      // We expect this to fail because HttpClient can't reach the image or OCR fails, but we just want to test IsUserAdjusted logic.
      // Wait, TranslateAdjustedBoxesAsync does downloading Image and OCR. If we mock everything:
      // It uses GetOcrService extracting image text.
      // So let's mock it to return something.
      ocrServiceMock.Setup(x => x.ExtractTextFromCroppedRegionAsync(It.IsAny<byte[]>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>()))
        .ReturnsAsync("New OCR Text");

      var request = new BoxTranslateRequest
      {
        PageId = page.PageId,
        Provider = "Google",
        OcrProvider = "server_tesseract",
        SourceLanguage = "ja",
        TargetLanguage = "vi",
        IsUserAdjusted = false,
        Boxes = new List<AdjustedBox>
        {
          new AdjustedBox 
          { 
             X = 10, Y = 10, Width = 100, Height = 50
          }
        }
      };

      await service.TranslateAdjustedBoxesAsync(request, 1);

      // Assert
      var updatedLayers = await db.PageTextLayers.Where(x => x.PageId == page.PageId).ToListAsync();
      
      updatedLayers.Should().HaveCount(1); // Should merge or replace
      var layer = updatedLayers.First();
      
      // Since it was UserAdjusted, the system should retain the original text/translation
      // or at least NOT lose the IsUserAdjusted flag and user's crucial modifications.
      layer.IsUserAdjusted.Should().BeTrue();
      // If the logic retains the old text on exact overlap when IsUserAdjusted is true:
      layer.OriginalText.Should().Be("Old Text");
    }
  }
}
