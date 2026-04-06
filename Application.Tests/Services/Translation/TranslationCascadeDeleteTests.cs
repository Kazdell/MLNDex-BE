using System.Linq;
using System.Threading.Tasks;
using Application.Tests.Shared;
using Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.Tests.Services.Translation
{
  [Collection("Database collection")]
  public class TranslationCascadeDeleteTests : IAsyncLifetime
  {
    private readonly DatabaseFixture _fixture;

    public TranslationCascadeDeleteTests(DatabaseFixture fixture)
    {
      _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
      await _fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DeleteTranslationTeam_ShouldThrow_WhenTeamHasTranslations()
    {
      // Arrange
      using var db = _fixture.CreateDbContext();

      var creator = new User { UserId = 1, Username = "creator", Email = "c@test.com", DisplayName = "Creator", PasswordHash = "X" };
      var leader = new User { UserId = 2, Username = "leader", Email = "l@test.com", DisplayName = "Leader", PasswordHash = "X" };
      db.Users.AddRange(creator, leader);
      
      var language = new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" };
      db.Languages.Add(language);
      
      var creatorProfile = new CreatorProfile { CreatorId = 1, UserId = 1, PenName = "Creator Pen" };
      db.CreatorProfiles.Add(creatorProfile);
      
      var series = new Series { SeriesId = 1, CreatorId = 1, Title = "Test Series" };
      db.Series.Add(series);
      
      var chapter = new Chapter { ChapterId = 1, SeriesId = 1, ChapterNumber = 1 };
      db.Chapters.Add(chapter);
      
      var team = new TranslationTeam { TeamName = "Test Team", Slug = "test-team", LeaderId = 2, LanguageId = 1 };
      db.TranslationTeams.Add(team);
      await db.SaveChangesAsync();

      var translation = new Domain.Entities.Translation 
      { 
        TranslationId = 1, 
        ChapterId = 1, 
        TeamId = team.TeamId,
        LanguageId = 1,
        IsOfficial = false 
      };
      db.Translations.Add(translation);
      await db.SaveChangesAsync();

      // Act & Assert
      db.TranslationTeams.Remove(team);
      
      // Since it's configured with DeleteBehavior.Restrict, EF Core will throw an exception
      // when attempting to delete a team that has related translations.
      // Wait, if it's Restrict, EF Core might prevent marking as deleted if it tracks the dependent entity, 
      // or DbUpdateException is thrown during SaveChangesAsync.
      var act = async () => await db.SaveChangesAsync();
      
      await act.Should().ThrowAsync<DbUpdateException>();
    }
  }
}
