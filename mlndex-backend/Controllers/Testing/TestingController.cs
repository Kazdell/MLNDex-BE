using Application.Interfaces.Data;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Testing
{
  [Route("api/[controller]")]
  [ApiController]
  public class TestingController : ControllerBase
  {
    private readonly IMlndexDbContext _db;

    public TestingController(IMlndexDbContext db)
    {
      _db = db;
    }

    [HttpPost("seed")]
    public async Task<IActionResult> SeedMockData()
    {
      try
      {
        // check if user exists
        if (_db.Users.Any(u => u.Email == "testuser1@test.com"))
        {
          return Ok(new { message = "Database already seeded. If you need new data, please drop local DB to run again." });
        }

        // Users
        var user1 = new Domain.Entities.User { Username = "testuser1", Email = "testuser1@test.com", DisplayName = "Test User 1", IsActive = true };
        var user2 = new Domain.Entities.User { Username = "creator1", Email = "creator1@test.com", DisplayName = "Creator One", IsActive = true };
        var transLeader = new Domain.Entities.User { Username = "trans_leader", Email = "translator1@test.com", DisplayName = "Trans Leader", IsActive = true };
        var transMember = new Domain.Entities.User { Username = "trans_member", Email = "translator2@test.com", DisplayName = "Trans Member", IsActive = true };
        _db.Users.AddRange(user1, user2, transLeader, transMember);
        await _db.SaveChangesAsync(default);

        // Creator Profile
        var creator = new CreatorProfile { UserId = user2.UserId, PenName = "Master Author", ReputationScore = 100, TotalRevenue = 0, IsActive = true, ModerationStatus = Domain.Entities.ModerationStatus.APPROVED };
        _db.CreatorProfiles.Add(creator);
        await _db.SaveChangesAsync(default);

        // Translation Team
        var team = new TranslationTeam { LeaderId = transLeader.UserId, TeamName = "Speed Scans", Description = "Fastest TL in the east", ReputationScore = 500, LockStatus = TeamLockStatus.ACTIVE, IsMonetizationEnabled = true, ModerationStatus = Domain.Entities.ModerationStatus.APPROVED };
        _db.TranslationTeams.Add(team);
        await _db.SaveChangesAsync(default);

        // Team Members
        var member1 = new TeamMember { TeamId = team.TeamId, UserId = transLeader.UserId, Role = TeamMemberRole.LEADER, JoinedAt = DateTime.UtcNow, IsActive = true };
        var member2 = new TeamMember { TeamId = team.TeamId, UserId = transMember.UserId, Role = TeamMemberRole.TRANSLATOR, JoinedAt = DateTime.UtcNow, IsActive = true };
        _db.TeamMembers.AddRange(member1, member2);
        await _db.SaveChangesAsync(default);

        // Genres (Comprehensive MangaDex-like List)
        var genreNames = new[] {
                    "Action", "Adventure", "Comedy", "Drama", "Slice of Life", "Fantasy",
                    "Magic", "Supernatural", "Horror", "Mystery", "Psychological", "Romance",
                    "Sci-Fi", "Cyberpunk", "Mecha", "Isekai", "Reincarnation", "Villainess",
                    "Historical", "Martial Arts", "Wuxia", "Xianxia", "Sports", "School Life",
                    "Thriller", "Seinen", "Shoujo", "Shounen", "Josei", "Harem", "Ecchi",
                    "Smut", "Yaoi", "Yuri", "Doujinshi", "Anthology", "Oneshot", "Full Color",
                    "Web Comic", "4-Koma", "Official Colored", "Gyaru", "Loli", "Shota",
                    "Monster Girls", "Superhero", "Adaptation", "Video Games", "Survival",
                    "Post-Apocalyptic", "Crime", "Cooking", "Music"
                };

        var genres = genreNames.Select(name => new Genre { Name = name }).ToList();
        _db.Genres.AddRange(genres);
        await _db.SaveChangesAsync(default);

        // Series
        var actionGenre = genres.First(g => g.Name == "Action");
        var fantasyGenre = genres.First(g => g.Name == "Fantasy");

        var series1 = new Domain.Entities.Series { CreatorId = creator.CreatorId, Title = "The Epic Journey", Description = "A very epic novel about a hero who fights demons.", SeriesFormat = SeriesFormat.NOVEL, AgeRating = AgeRating.TEEN, Status = SeriesStatus.ONGOING, ModerationStatus = Domain.Entities.ModerationStatus.APPROVED, AverageRating = 4.5m, TotalRatings = 10, CreatedAt = DateTime.UtcNow };
        var series2 = new Domain.Entities.Series { CreatorId = creator.CreatorId, Title = "Manga Adventures", Description = "Action manga with lots of fights and character building.", SeriesFormat = SeriesFormat.MANGA, AgeRating = AgeRating.ALL, Status = SeriesStatus.COMPLETED, ModerationStatus = Domain.Entities.ModerationStatus.APPROVED, AverageRating = 4.8m, TotalRatings = 50, CreatedAt = DateTime.UtcNow.AddDays(-10) };
        _db.Series.AddRange(series1, series2);
        await _db.SaveChangesAsync(default);

        // SeriesGenres
        _db.SeriesGenres.Add(new SeriesGenre { SeriesId = series1.SeriesId, GenreId = fantasyGenre.GenreId });
        _db.SeriesGenres.Add(new SeriesGenre { SeriesId = series2.SeriesId, GenreId = actionGenre.GenreId });
        await _db.SaveChangesAsync(default);

        // Chapters
        var chap1 = new Domain.Entities.Chapter { SeriesId = series1.SeriesId, ChapterNumber = 1, Title = "The Beginning", ContentType = ContentType.TEXT, LockStatus = ChapterLockStatus.FREE, Status = ChapterStatus.PUBLISHED, ModerationStatus = Domain.Entities.ModerationStatus.APPROVED, PublishedAt = DateTime.UtcNow, TeamId = team.TeamId }; // assigned translation team
        var chap2 = new Domain.Entities.Chapter { SeriesId = series2.SeriesId, ChapterNumber = 1, Title = "Manga Chap 1", ContentType = ContentType.IMAGE, LockStatus = ChapterLockStatus.FREE, Status = ChapterStatus.PUBLISHED, ModerationStatus = Domain.Entities.ModerationStatus.APPROVED, PublishedAt = DateTime.UtcNow };
        _db.Chapters.AddRange(chap1, chap2);
        await _db.SaveChangesAsync(default);

        // Notifications
        var notif1 = new Domain.Entities.Notification { UserId = user1.UserId, NotificationType = NotificationType.NEW_SERIES, Title = "New Series Alert", Message = "A new series was published", ActionUrl = "/series/" + series1.SeriesId, IsRead = false, CreatedAt = DateTime.UtcNow };
        var notif2 = new Domain.Entities.Notification { UserId = user1.UserId, NotificationType = NotificationType.NEW_CHAPTER, Title = "New Chapter", Message = "Chap 1 is out!", ActionUrl = "/chapter/" + chap1.ChapterId, IsRead = false, CreatedAt = DateTime.UtcNow };
        _db.Notifications.AddRange(notif1, notif2);
        await _db.SaveChangesAsync(default);

        return Ok(new
        {
          Message = "Mock Data Seeded Successfully",
          TotalGenresAdded = genres.Count,
          SeriesList = new[] { series1.SeriesId, series2.SeriesId },
          TestUserId = user1.UserId,
          TranslationTeam = team.TeamId
        });
      }
      catch (Exception ex)
      {
        return BadRequest(ex.Message);
      }
    }
  }
}
