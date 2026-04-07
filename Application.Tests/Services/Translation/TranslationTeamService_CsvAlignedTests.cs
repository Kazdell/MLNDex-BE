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
using Xunit.Abstractions;

namespace Application.Tests.Services.Translation;

public class TranslationTeamService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;
  private readonly Mock<IUserContext> _mockUserContext = new();
  private readonly Mock<INotificationService> _mockNotificationService = new();

  public TranslationTeamService_CsvAlignedTests(ITestOutputHelper output)
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

  private static async Task SeedUserAndLanguageAsync(MlndexDbContext db, int userId = 1)
  {
    db.Users.Add(new User
    {
      UserId = userId,
      Username = $"user{userId}",
      Email = $"u{userId}@test.com",
      DisplayName = $"User {userId}",
      PasswordHash = "hash"
    });
    db.Languages.Add(new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" });
    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task CreateTeamAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserAndLanguageAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(10);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var input = new CreateTranslationTeamDto
    {
      TeamName = "Alpha Team",
      Slug = "alpha-team",
      Description = "Core translators",
      LanguageId = 1,
      RequireApproval = true
    };

    var output = await service.CreateTeamAsync(input);

    _output.WriteLine($"Input: TeamName={input.TeamName}, LeaderId=10");
    _output.WriteLine($"Output: TeamId={output.TeamId}, TeamName={output.TeamName}");

    output.TeamName.Should().Be("Alpha Team");
    (await db.TeamMembers.CountAsync(m => m.TeamId == output.TeamId)).Should().Be(1);
  }

  [Fact]
  public async Task CreateTeamAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserAndLanguageAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(null as int?);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateTeamAsync(new CreateTranslationTeamDto
    {
      TeamName = "Alpha Team",
      Slug = "alpha-team"
    }));

    _output.WriteLine("Input: UserId=null");
    _output.WriteLine($"Output Exception: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task CreateTeamAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserAndLanguageAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(10);

    db.TranslationTeams.Add(new TranslationTeam
    {
      TeamId = 1,
      LeaderId = 10,
      TeamName = "Alpha Team",
      Slug = "existing-slug"
    });
    await db.SaveChangesAsync();

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAsync<Exception>(() => service.CreateTeamAsync(new CreateTranslationTeamDto
    {
      TeamName = "Alpha Team",
      Slug = "alpha-team"
    }));

    _output.WriteLine("Input: duplicate team name");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Be("Team name already exists.");
  }

  [Fact]
  public async Task UpdateTeamAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserAndLanguageAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(10);

    db.TranslationTeams.Add(new TranslationTeam
    {
      TeamId = 2,
      LeaderId = 10,
      TeamName = "Old Team",
      Slug = "old-team",
      LanguageId = 1
    });
    await db.SaveChangesAsync();

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.UpdateTeamAsync(2, new UpdateTranslationTeamDto
    {
      TeamName = "New Team",
      Slug = "new-team",
      Description = "updated"
    });

    _output.WriteLine("Input: teamId=2 update name/slug");
    _output.WriteLine($"Output: TeamName={output.TeamName}, Slug={output.Slug}");

    output.TeamName.Should().Be("New Team");
    output.Slug.Should().Be("new-team");
  }

  [Fact]
  public async Task UpdateTeamAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserAndLanguageAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(10);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAsync<Exception>(() => service.UpdateTeamAsync(-1, new UpdateTranslationTeamDto
    {
      TeamName = "X"
    }));

    _output.WriteLine("Input: teamId=-1");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Be("Team not found or unauthorized.");
  }

  [Fact]
  public async Task UpdateTeamAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserAndLanguageAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(10);

    db.TranslationTeams.Add(new TranslationTeam
    {
      TeamId = 2,
      LeaderId = 10,
      TeamName = "Old Team",
      Slug = "old-team"
    });
    await db.SaveChangesAsync();

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.UpdateTeamAsync(2, null!));

    _output.WriteLine("Input: updateDto=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task DisbandTeamAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserAndLanguageAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(10);

    db.TranslationTeams.Add(new TranslationTeam { TeamId = 11, LeaderId = 10, TeamName = "ToRemove", Slug = "to-remove" });
    db.TeamMembers.Add(new TeamMember { TeamId = 11, UserId = 10, IsActive = true, JoinedAt = DateTime.UtcNow, Role = TeamMemberRole.LEADER });
    await db.SaveChangesAsync();

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.DisbandTeamAsync(11);

    _output.WriteLine("Input: disband teamId=11");
    _output.WriteLine($"Output: {output}");

    output.Should().BeTrue();
    (await db.TranslationTeams.AnyAsync(t => t.TeamId == 11)).Should().BeFalse();
  }

  [Fact]
  public async Task DisbandTeamAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserAndLanguageAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Returns(10);

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.DisbandTeamAsync(-1);

    _output.WriteLine("Input: teamId=-1");
    _output.WriteLine($"Output: {output}");

    output.Should().BeFalse();
  }

  [Fact]
  public async Task DisbandTeamAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserAndLanguageAsync(db, 10);
    _mockUserContext.Setup(x => x.UserId).Throws(new Exception("User context failure"));

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var ex = await Assert.ThrowsAsync<Exception>(() => service.DisbandTeamAsync(1));

    _output.WriteLine("Input: user context throws");
    _output.WriteLine($"Output Exception: {ex.Message}");
    ex.Message.Should().Be("User context failure");
  }

  [Fact]
  public async Task GetTeamByIdAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserAndLanguageAsync(db, 10);

    db.Genres.Add(new Genre { GenreId = 1, Name = "Action" });
    db.TranslationTeams.Add(new TranslationTeam { TeamId = 21, LeaderId = 10, TeamName = "ReadTeam", Slug = "read-team" });
    db.TeamGenres.Add(new TeamGenre { TeamId = 21, GenreId = 1 });
    db.TeamMembers.Add(new TeamMember { TeamId = 21, UserId = 10, Role = TeamMemberRole.LEADER, IsActive = true, JoinedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = await service.GetTeamByIdAsync(21);

    _output.WriteLine("Input: teamId=21");
    _output.WriteLine($"Output: TeamName={output?.TeamName}, MemberCount={output?.MemberCount}");

    output.Should().NotBeNull();
    output!.TeamName.Should().Be("ReadTeam");
  }

  [Fact]
  public async Task GetTeamByIdAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var output = await service.GetTeamByIdAsync(-1);

    _output.WriteLine("Input: teamId=-1");
    _output.WriteLine($"Output: {(output == null ? "null" : output.TeamName)}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task GetTeamByIdAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetTeamByIdAsync(1));

    _output.WriteLine("Input: disposed db context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetAllTeamsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserAndLanguageAsync(db, 10);

    db.TranslationTeams.Add(new TranslationTeam { TeamId = 31, LeaderId = 10, TeamName = "T1", Slug = "t1" });
    db.TranslationTeams.Add(new TranslationTeam { TeamId = 32, LeaderId = 10, TeamName = "T2", Slug = "t2" });
    await db.SaveChangesAsync();

    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    var output = (await service.GetAllTeamsAsync()).ToList();

    _output.WriteLine("Input: query all teams");
    _output.WriteLine($"Output count: {output.Count}");

    output.Count.Should().Be(2);
  }

  [Fact]
  public async Task GetAllTeamsAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);

    var output = (await service.GetAllTeamsAsync()).ToList();

    _output.WriteLine("Input: empty database");
    _output.WriteLine($"Output count: {output.Count}");

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetAllTeamsAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(async () => _ = (await service.GetAllTeamsAsync()).ToList());

    _output.WriteLine("Input: disposed context");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");
    ex.Should().NotBeNull();
  }
}
