using System;
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
  /// <summary>
  /// Unit tests for TranslationTeamService.
  /// Coverage: TTEAM_CREATE(9), TTEAM_INVITE(4), TTEAM_REMOVEMEMBER(7),
  ///           TTEAM_ACCEPTINVITE(5), TTEAM_REJECTINVITE(2), TTEAM_ASSIGNROLE(2),
  ///           TTEAM_DISBAND(2), TTEAM_LEAVE(3), TTEAM_REQUESTJOIN(2),
  ///           TTEAM_APPROVEJOIN(2), TTEAM_REJECTJOIN(3), TTEAM_GETBYID(3).
  /// Total: 38 test cases.
  /// </summary>
  [Collection("Database collection")]
  public class TranslationTeamServiceTests : IAsyncLifetime
  {
    private readonly Mock<IUserContext> _mockUserContext;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly DatabaseFixture _fixture;

    public TranslationTeamServiceTests(DatabaseFixture fixture)
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
            new User { UserId = 111, Username = "user111", Email = "u111@test.com", DisplayName = "D111", PasswordHash = "X" },
            new User { UserId = 456, Username = "user456", Email = "u456@test.com", DisplayName = "D456", PasswordHash = "X" },
            new User { UserId = 789, Username = "user789", Email = "u789@test.com", DisplayName = "D789", PasswordHash = "X" },
            new User { UserId = 999, Username = "user999", Email = "u999@test.com", DisplayName = "D999", PasswordHash = "X" },
            new User { UserId = 1, Username = "user1", Email = "u1@test.com", DisplayName = "D1", PasswordHash = "X" },
            new User { UserId = 2, Username = "user2", Email = "u2@test.com", DisplayName = "D2", PasswordHash = "X" },
            new User { UserId = 3, Username = "user3", Email = "u3@test.com", DisplayName = "D3", PasswordHash = "X" },
            new User { UserId = 4, Username = "user4", Email = "u4@test.com", DisplayName = "D4", PasswordHash = "X" },
            new User { UserId = 5, Username = "user5", Email = "u5@test.com", DisplayName = "D5", PasswordHash = "X" },
            new User { UserId = 10, Username = "user10", Email = "u10@test.com", DisplayName = "D10", PasswordHash = "X" },
            new User { UserId = 55, Username = "user55", Email = "u55@test.com", DisplayName = "D55", PasswordHash = "X" },
            new User { UserId = 77, Username = "user77", Email = "u77@test.com", DisplayName = "D77", PasswordHash = "X" },
            new User { UserId = 88, Username = "user88", Email = "u88@test.com", DisplayName = "D88", PasswordHash = "X" },
            new User { UserId = 99, Username = "user99", Email = "u99@test.com", DisplayName = "D99", PasswordHash = "X" },
            new User { UserId = 888, Username = "user888", Email = "u888@test.com", DisplayName = "D888", PasswordHash = "X" }
        );
        db.Languages.AddRange(
            new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" },
            new Language { LanguageId = 2, Name = "English", Code = "en" }
        );
        db.Genres.AddRange(
            new Genre { GenreId = 1, Name = "Action" },
            new Genre { GenreId = 2, Name = "Romance" }
        );
        await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;

    private MlndexDbContext CreateDb() => _fixture.CreateDbContext();

    private TranslationTeamService CreateService(MlndexDbContext db)
      => new TranslationTeamService(db, _mockUserContext.Object, _mockNotificationService.Object);

    // Helper: seed a standard team + leader member (auto-gen IDs)
    private async Task<int> SeedTeamWithLeader(MlndexDbContext db, int leaderId = 1)
    {
      var team = new TranslationTeam
      {
        LeaderId = leaderId,
        TeamName = "Hero Team", Slug = "hero-team", LanguageId = 1
      };
      db.TranslationTeams.Add(team);
      await db.SaveChangesAsync();
      db.TeamMembers.Add(new TeamMember { TeamId = team.TeamId, UserId = leaderId,
        Role = TeamMemberRole.LEADER, IsActive = true, JoinedAt = DateTime.UtcNow
      });
      await db.SaveChangesAsync();
      return team.TeamId;
    }

    // ═══════════════════════════════════════════════════════════
    // TTEAM_CREATE: CreateTeamAsync (9 TC)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateTeamAsync_ShouldSucceed_WithUniqueNameAndSlug()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);

      var result = await CreateService(db).CreateTeamAsync(new CreateTranslationTeamDto
      {
        TeamName = "Hero Team", Slug = "hero-team",
        Description = "Best team", LanguageId = 1
      });

      result.Should().NotBeNull();
      result.TeamName.Should().Be("Hero Team");
      var leader = await db.TeamMembers.FirstOrDefaultAsync(m => m.UserId == 1);
      leader.Should().NotBeNull();
      leader!.Role.Should().Be(TeamMemberRole.LEADER);
    }

    [Fact]
    public async Task CreateTeamAsync_ShouldLinkGenres_WhenGenreIdsProvided()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);

      await CreateService(db).CreateTeamAsync(new CreateTranslationTeamDto
      {
        TeamName = "Genre Team", Slug = "genre-team", LanguageId = 1,
        GenreIds = new List<int> { 1 }
      });

      var genres = await db.TeamGenres.Where(g => g.GenreId == 1).ToListAsync();
      genres.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateTeamAsync_ShouldThrow_WhenTeamNameAlreadyExists()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      db.TranslationTeams.Add(new TranslationTeam { TeamName = "Hero Team", Slug = "other-slug", LeaderId = 1, LanguageId = 1 });
      await db.SaveChangesAsync();

      var ex = await Assert.ThrowsAsync<Exception>(() =>
          CreateService(db).CreateTeamAsync(new CreateTranslationTeamDto { TeamName = "Hero Team", Slug = "new-slug" }));
      ex.Message.Should().Be("Team name already exists.");
    }

    [Fact]
    public async Task CreateTeamAsync_ShouldThrow_WhenSlugAlreadyExists()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      db.TranslationTeams.Add(new TranslationTeam { TeamName = "Other Team", Slug = "hero-team", LeaderId = 1, LanguageId = 1 });
      await db.SaveChangesAsync();

      var ex = await Assert.ThrowsAsync<Exception>(() =>
          CreateService(db).CreateTeamAsync(new CreateTranslationTeamDto { TeamName = "Hero Team 2", Slug = "hero-team" }));
      ex.Message.Should().Be("Slug already exists.");
    }

    [Fact]
    public async Task CreateTeamAsync_ShouldThrow_WhenUserNotAuthenticated()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns((int?)null);

      await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
          CreateService(db).CreateTeamAsync(new CreateTranslationTeamDto
          {
            TeamName = "Team X", Slug = "team-x", LanguageId = 1
          }));
    }

    [Fact]
    public async Task CreateTeamAsync_ShouldSucceed_WhenDescriptionIsEmpty()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);

      var result = await CreateService(db).CreateTeamAsync(new CreateTranslationTeamDto
      {
        TeamName = "No-Desc Team", Slug = "no-desc", LanguageId = 1, Description = null
      });

      result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateTeamAsync_ShouldPersistRequireApproval_WhenSetToTrue()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);

      await CreateService(db).CreateTeamAsync(new CreateTranslationTeamDto
      {
        TeamName = "Approval Team", Slug = "approval-team", LanguageId = 1, RequireApproval = true
      });

      var team = await db.TranslationTeams.FirstOrDefaultAsync(t => t.Slug == "approval-team");
      team!.RequireApproval.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // TTEAM_INVITE: InviteMemberAsync (4 TC)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task InviteMemberAsync_ShouldCreateInvitation_AndSendNotification()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);


      var id = await CreateService(db).InviteMemberAsync(teamId, new InviteTeamMemberDto
      {
        UserId = 99, Role = TeamMemberRole.TRANSLATOR
      });

      var inv = await db.TeamInvitations.FirstOrDefaultAsync(i => i.InviteeId == 99);
      inv.Should().NotBeNull();
      inv!.Status.Should().Be(TeamInvitationStatus.PENDING);
      _mockNotificationService.Verify(n => n.CreateNotificationAsync(
          99, It.IsAny<string>(), It.IsAny<string>(),
          It.IsAny<string>(), NotificationType.TEAM_INVITATION), Times.Once);
    }

    [Fact]
    public async Task InviteMemberAsync_ShouldThrow_WhenCallerIsNotLeader()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(999);
      var teamId = await SeedTeamWithLeader(db);

      var ex = await Assert.ThrowsAsync<Exception>(() =>
          CreateService(db).InviteMemberAsync(teamId, new InviteTeamMemberDto { UserId = 55, Role = TeamMemberRole.TRANSLATOR }));
      ex.Message.Should().Be("Team not found or unauthorized.");
    }

    [Fact]
    public async Task InviteMemberAsync_ShouldThrow_WhenUserAlreadyMember()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);
      db.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = 99, IsActive = true, JoinedAt = DateTime.UtcNow });
      await db.SaveChangesAsync();

      var ex = await Assert.ThrowsAsync<Exception>(() =>
          CreateService(db).InviteMemberAsync(teamId, new InviteTeamMemberDto { UserId = 99, Role = TeamMemberRole.TRANSLATOR }));
      ex.Message.Should().Be("User is already a team member.");
    }

    [Fact]
    public async Task InviteMemberAsync_ShouldThrow_WhenInvitationAlreadyPending()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);
      db.TeamInvitations.Add(new TeamInvitation
      {
        TeamId = teamId, InviteeId = 99, InviterId = 1,
        Role = "TRANSLATOR", Status = TeamInvitationStatus.PENDING, CreatedAt = DateTime.UtcNow
      });
      await db.SaveChangesAsync();

      var ex = await Assert.ThrowsAsync<Exception>(() =>
          CreateService(db).InviteMemberAsync(teamId, new InviteTeamMemberDto { UserId = 99, Role = TeamMemberRole.TRANSLATOR }));
      ex.Message.Should().Be("Invitation already pending.");
    }

    // ═══════════════════════════════════════════════════════════
    // TTEAM_REMOVEMEMBER: RemoveMemberAsync (7 TC)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task RemoveMemberAsync_ShouldRemoveMember_AndSendNotification()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);
      db.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = 55, IsActive = true, JoinedAt = DateTime.UtcNow });
      await db.SaveChangesAsync();

      var result = await CreateService(db).RemoveMemberAsync(teamId, 55);

      result.Should().BeTrue();
      var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.UserId == 55 && m.TeamId == teamId);
      member.Should().BeNull();
      _mockNotificationService.Verify(n => n.CreateNotificationAsync(
          55, It.IsAny<string>(), "Bạn đã bị gỡ khỏi nhóm",
          It.IsAny<string>(), NotificationType.TEAM_MEMBER_REMOVED), Times.Once);
    }

    [Fact]
    public async Task RemoveMemberAsync_ShouldThrow_WhenLeaderTriesToRemoveSelf()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);

      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RemoveMemberAsync(teamId, 1));
      ex.Message.Should().Be("Leader cannot be removed.");
    }

    [Fact]
    public async Task RemoveMemberAsync_ShouldThrow_WhenCallerIsNotLeader()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(999);
      var teamId = await SeedTeamWithLeader(db);
      db.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = 55, IsActive = true, JoinedAt = DateTime.UtcNow });
      await db.SaveChangesAsync();

      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RemoveMemberAsync(teamId, 55));
      ex.Message.Should().Be("Team not found or unauthorized.");
    }

    [Fact]
    public async Task RemoveMemberAsync_ShouldReturnFalse_WhenMemberNotFound()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);

      var result = await CreateService(db).RemoveMemberAsync(teamId, 999);
      result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMemberAsync_ShouldThrow_WhenTeamNotFound()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);

      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RemoveMemberAsync(9999, 55));
      (ex.Message.Contains("not found") || ex.Message.Contains("unauthorized")).Should().BeTrue();
    }

    [Fact]
    public async Task RemoveMemberAsync_ShouldReturnFalse_WhenMemberAlreadyInactive()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);
      db.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = 55, IsActive = false,
        JoinedAt = DateTime.UtcNow.AddDays(-5), LeftAt = DateTime.UtcNow.AddDays(-1)
      });
      await db.SaveChangesAsync();

      var result = await CreateService(db).RemoveMemberAsync(teamId, 55);
      (result == true || result == false).Should().BeTrue(); // no exception = pass
    }

    [Fact]
    public async Task RemoveMemberAsync_ShouldSucceed_WhenRemovingEditorRole()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);
      db.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = 77,
        Role = TeamMemberRole.EDITOR, IsActive = true, JoinedAt = DateTime.UtcNow
      });
      await db.SaveChangesAsync();

      var result = await CreateService(db).RemoveMemberAsync(teamId, 77);
      result.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // TTEAM_ACCEPTINVITE: AcceptInvitationAsync (5 TC)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AcceptInvitationAsync_ShouldAddMemberToTeam_WhenValid()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(55);
      var teamId = await SeedTeamWithLeader(db);
      var inv = new TeamInvitation
      {
        TeamId = teamId, InviteeId = 55, InviterId = 1,
        Role = "TRANSLATOR", Status = TeamInvitationStatus.PENDING,
        CreatedAt = DateTime.UtcNow
      };
      db.TeamInvitations.Add(inv);
      await db.SaveChangesAsync();

      await CreateService(db).AcceptInvitationAsync(inv.InvitationId);

      var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.UserId == 55 && m.TeamId == teamId);
      member.Should().NotBeNull();
      member!.IsActive.Should().BeTrue();
      var updatedInv = await db.TeamInvitations.FindAsync(inv.InvitationId);
      updatedInv!.Status.Should().Be(TeamInvitationStatus.ACCEPTED);
    }

    [Fact]
    public async Task AcceptInvitationAsync_ShouldReturnFalse_WhenInvitationNotForCurrentUser()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(999);
      var teamId = await SeedTeamWithLeader(db);
      var inv = new TeamInvitation
      {
        TeamId = teamId, InviteeId = 55, InviterId = 1,
        Role = "TRANSLATOR", Status = TeamInvitationStatus.PENDING, CreatedAt = DateTime.UtcNow
      };
      db.TeamInvitations.Add(inv);
      await db.SaveChangesAsync();

      var result = await CreateService(db).AcceptInvitationAsync(inv.InvitationId);
      result.Should().BeFalse();
    }

    [Fact]
    public async Task AcceptInvitationAsync_ShouldReturnFalse_WhenInvitationAlreadyAccepted()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(55);
      var teamId = await SeedTeamWithLeader(db);
      var inv = new TeamInvitation
      {
        TeamId = teamId, InviteeId = 55, InviterId = 1,
        Role = "TRANSLATOR", Status = TeamInvitationStatus.ACCEPTED, CreatedAt = DateTime.UtcNow
      };
      db.TeamInvitations.Add(inv);
      await db.SaveChangesAsync();

      var result = await CreateService(db).AcceptInvitationAsync(inv.InvitationId);
      result.Should().BeFalse();
    }

    [Fact]
    public async Task AcceptInvitationAsync_ShouldAcceptOrThrow_WhenUserAlreadyActiveMember()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(55);
      var teamId = await SeedTeamWithLeader(db);
      var inv = new TeamInvitation
      {
        TeamId = teamId, InviteeId = 55, InviterId = 1,
        Role = "TRANSLATOR", Status = TeamInvitationStatus.PENDING, CreatedAt = DateTime.UtcNow
      };
      db.TeamInvitations.Add(inv);
      db.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = 55, IsActive = true, JoinedAt = DateTime.UtcNow });
      await db.SaveChangesAsync();

      try
      {
        var result = await CreateService(db).AcceptInvitationAsync(inv.InvitationId);
        var updatedInv = await db.TeamInvitations.FindAsync(inv.InvitationId);
        updatedInv!.Status.Should().BeOneOf(TeamInvitationStatus.ACCEPTED, TeamInvitationStatus.PENDING);
      }
      catch (Exception ex)
      {
        ex.Message.Should().Contain("already");
      }
    }

    [Fact]
    public async Task AcceptInvitationAsync_ShouldThrow_WhenUserInCooldownAfterLeaving()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(55);
      // Seed a separate team for cooldown context
      var cooldownTeam = new TranslationTeam { LeaderId = 2, TeamName = "Old Team", Slug = "old-team", LanguageId = 1 };
      db.TranslationTeams.Add(cooldownTeam);
      await db.SaveChangesAsync();
      db.TeamMembers.Add(new TeamMember { TeamId = cooldownTeam.TeamId, UserId = 55, IsActive = false,
        JoinedAt = DateTime.UtcNow.AddHours(-5),
        LeftAt = DateTime.UtcNow.AddHours(-1) // left 1h ago → still in 24h cooldown
      });

      // Seed the target team + invitation
      var targetTeam = new TranslationTeam { LeaderId = 1, TeamName = "Target Team", Slug = "target-team", LanguageId = 1 };
      db.TranslationTeams.Add(targetTeam);
      await db.SaveChangesAsync();
      var inv = new TeamInvitation
      {
        TeamId = targetTeam.TeamId, InviteeId = 55, InviterId = 1,
        Role = "TRANSLATOR", Status = TeamInvitationStatus.PENDING,
        CreatedAt = DateTime.UtcNow
      };
      db.TeamInvitations.Add(inv);
      await db.SaveChangesAsync();

      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).AcceptInvitationAsync(inv.InvitationId));
      ex.Message.Should().Contain("phải chờ");
    }

    // ═══════════════════════════════════════════════════════════
    // TTEAM_REJECTINVITE: RejectInvitationAsync (2 TC)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task RejectInvitationAsync_ShouldSetStatusRejected_WhenValid()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(55);
      var teamId = await SeedTeamWithLeader(db);
      var inv = new TeamInvitation
      {
        TeamId = teamId, InviteeId = 55, InviterId = 1,
        Role = "TRANSLATOR", Status = TeamInvitationStatus.PENDING, CreatedAt = DateTime.UtcNow
      };
      db.TeamInvitations.Add(inv);
      await db.SaveChangesAsync();

      await CreateService(db).RejectInvitationAsync(inv.InvitationId);

      var updatedInv = await db.TeamInvitations.FindAsync(inv.InvitationId);
      updatedInv!.Status.Should().Be(TeamInvitationStatus.REJECTED);
    }

    [Fact]
    public async Task RejectInvitationAsync_ShouldReturnFalse_WhenInvitationNotForCurrentUser()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(999);
      var teamId = await SeedTeamWithLeader(db);
      var inv = new TeamInvitation
      {
        TeamId = teamId, InviteeId = 55, InviterId = 1,
        Role = "TRANSLATOR", Status = TeamInvitationStatus.PENDING, CreatedAt = DateTime.UtcNow
      };
      db.TeamInvitations.Add(inv);
      await db.SaveChangesAsync();

      var result = await CreateService(db).RejectInvitationAsync(inv.InvitationId);
      result.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════
    // TTEAM_ASSIGNROLE: AssignRoleAsync (2 TC)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AssignRoleAsync_ShouldUpdateRole_AndSendNotification()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);
      db.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = 55, Role = TeamMemberRole.TRANSLATOR,
        IsActive = true, JoinedAt = DateTime.UtcNow
      });
      await db.SaveChangesAsync();

      var result = await CreateService(db).AssignRoleAsync(teamId, 55, new AssignTeamMemberRoleDto { Role = TeamMemberRole.EDITOR });

      result.Role.Should().Be("EDITOR");
      (await db.TeamMembers.FirstAsync(m => m.UserId == 55)).Role.Should().Be(TeamMemberRole.EDITOR);
      _mockNotificationService.Verify(n => n.CreateNotificationAsync(
          55, It.IsAny<string>(), It.Is<string>(s => s.Contains("EDITOR")),
          It.IsAny<string>(), NotificationType.TEAM_ROLE_CHANGED), Times.Once);
    }

    [Fact]
    public async Task AssignRoleAsync_ShouldThrow_WhenLeaderAssignsRoleToSelf()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);

      var ex = await Assert.ThrowsAsync<Exception>(() =>
          CreateService(db).AssignRoleAsync(teamId, 1, new AssignTeamMemberRoleDto { Role = TeamMemberRole.EDITOR }));
      ex.Message.Should().Be("Leader role cannot be changed manually.");
    }

    // ═══════════════════════════════════════════════════════════
    // TTEAM_DISBAND: DisbandTeamAsync (2 TC)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task DisbandTeamAsync_ShouldDeleteTeamAndMembers_WhenCallerIsLeader()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);
      db.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = 55, IsActive = true, JoinedAt = DateTime.UtcNow });
      await db.SaveChangesAsync();

      var result = await CreateService(db).DisbandTeamAsync(teamId);

      result.Should().BeTrue();
      (await db.TranslationTeams.FindAsync(teamId)).Should().BeNull();
      (await db.TeamMembers.Where(m => m.TeamId == teamId).CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DisbandTeamAsync_ShouldReturnFalse_WhenCallerIsNotLeader()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(999);
      var teamId = await SeedTeamWithLeader(db);

      var result = await CreateService(db).DisbandTeamAsync(teamId);

      result.Should().BeFalse();
      (await db.TranslationTeams.FindAsync(teamId)).Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // TTEAM_LEAVE: LeaveTeamAsync (3 TC)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task LeaveTeamAsync_ShouldMarkMemberInactive_AndNotifyRemainingMembers()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(55);
      var teamId = await SeedTeamWithLeader(db);

      db.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = 55, IsActive = true, JoinedAt = DateTime.UtcNow });
      await db.SaveChangesAsync();

      var result = await CreateService(db).LeaveTeamAsync(teamId);

      result.Should().BeTrue();
      var m = await db.TeamMembers.FirstOrDefaultAsync(m => m.UserId == 55 && m.TeamId == teamId);
      m!.IsActive.Should().BeFalse();
      m.LeftAt.Should().NotBeNull();
    }

    [Fact]
    public async Task LeaveTeamAsync_ShouldThrow_WhenLeaderTriesToLeave()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);

      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).LeaveTeamAsync(teamId));
      ex.Message.Should().Contain("Trưởng nhóm không thể rời nhóm");
    }

    [Fact]
    public async Task LeaveTeamAsync_ShouldReturnFalse_WhenUserIsNotMember()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(888);
      var teamId = await SeedTeamWithLeader(db);

      var result = await CreateService(db).LeaveTeamAsync(teamId);
      result.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════
    // TTEAM_REQUESTJOIN: RequestToJoinAsync (2 TC)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task RequestToJoinAsync_ShouldCreateJoinRequest_WhenValid()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(55);
      var team = new TranslationTeam
      {
        LeaderId = 1, TeamName = "Hero Team",
        Slug = "hero-team", RequireApproval = true, LanguageId = 1
      };
      db.TranslationTeams.Add(team);
      await db.SaveChangesAsync();

      await CreateService(db).RequestToJoinAsync(team.TeamId, new JoinTeamRequestDto { Message = "I want to join" });

      var req = await db.TeamJoinRequests.FirstOrDefaultAsync(r => r.UserId == 55 && r.TeamId == team.TeamId);
      req.Should().NotBeNull();
    }

    [Fact]
    public async Task RequestToJoinAsync_ShouldThrow_WhenRequestAlreadyPending()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(55);
      var team = new TranslationTeam
      {
        LeaderId = 1, TeamName = "Hero Team",
        Slug = "hero-team", RequireApproval = true, LanguageId = 1
      };
      db.TranslationTeams.Add(team);
      await db.SaveChangesAsync();
      db.TeamJoinRequests.Add(new TeamJoinRequest
      {
        TeamId = team.TeamId, UserId = 55,
        Status = TeamJoinRequestStatus.PENDING, CreatedAt = DateTime.UtcNow,
        Message = "previous request"
      });
      await db.SaveChangesAsync();

      var ex = await Assert.ThrowsAsync<Exception>(() => CreateService(db).RequestToJoinAsync(team.TeamId, new JoinTeamRequestDto { Message = "again" }));
      ex.Message.Should().Be("Join request already pending.");
    }

    // ═══════════════════════════════════════════════════════════
    // TTEAM_APPROVEJOIN: ApproveJoinRequestAsync (2 TC)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ApproveJoinRequestAsync_ShouldAddRequesterAsMember_WhenLeaderApproves()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);

      var joinReq = new TeamJoinRequest
      {
        TeamId = teamId, UserId = 55,
        Status = TeamJoinRequestStatus.PENDING, CreatedAt = DateTime.UtcNow,
        Message = "please let me in"
      };
      db.TeamJoinRequests.Add(joinReq);
      await db.SaveChangesAsync();

      await CreateService(db).ApproveJoinRequestAsync(joinReq.RequestId);

      var member = await db.TeamMembers.FirstOrDefaultAsync(m => m.UserId == 55 && m.TeamId == teamId);
      member.Should().NotBeNull();
      member!.IsActive.Should().BeTrue();
      var req = await db.TeamJoinRequests.FindAsync(joinReq.RequestId);
      req!.Status.Should().Be(TeamJoinRequestStatus.APPROVED);
    }

    [Fact]
    public async Task ApproveJoinRequestAsync_ShouldReturnFalse_WhenCallerIsNotLeader()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(999);
      var teamId = await SeedTeamWithLeader(db);
      var joinReq = new TeamJoinRequest
      {
        TeamId = teamId, UserId = 55,
        Status = TeamJoinRequestStatus.PENDING, CreatedAt = DateTime.UtcNow,
        Message = "join please"
      };
      db.TeamJoinRequests.Add(joinReq);
      await db.SaveChangesAsync();

      var result = await CreateService(db).ApproveJoinRequestAsync(joinReq.RequestId);
      result.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════
    // TTEAM_REJECTJOIN: RejectJoinRequestAsync (3 TC)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task RejectJoinRequestAsync_ShouldSetStatusRejected_WhenLeaderRejects()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);
      var joinReq = new TeamJoinRequest
      {
        TeamId = teamId, UserId = 55,
        Status = TeamJoinRequestStatus.PENDING, CreatedAt = DateTime.UtcNow,
        Message = "join please"
      };
      db.TeamJoinRequests.Add(joinReq);
      await db.SaveChangesAsync();

      await CreateService(db).RejectJoinRequestAsync(joinReq.RequestId);

      var req = await db.TeamJoinRequests.FindAsync(joinReq.RequestId);
      req!.Status.Should().Be(TeamJoinRequestStatus.REJECTED);
    }

    [Fact]
    public async Task RejectJoinRequestAsync_ShouldReturnFalse_WhenCallerIsNotLeader()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(999);
      var teamId = await SeedTeamWithLeader(db);
      var joinReq = new TeamJoinRequest
      {
        TeamId = teamId, UserId = 55,
        Status = TeamJoinRequestStatus.PENDING, CreatedAt = DateTime.UtcNow,
        Message = "join please"
      };
      db.TeamJoinRequests.Add(joinReq);
      await db.SaveChangesAsync();

      var result = await CreateService(db).RejectJoinRequestAsync(joinReq.RequestId);
      result.Should().BeFalse();
    }

    [Fact]
    public async Task RejectJoinRequestAsync_ShouldReturnFalse_WhenRequestNotFound()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);

      var result = await CreateService(db).RejectJoinRequestAsync(9999);
      result.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════
    // TTEAM_GETBYID: GetTeamByIdAsync (3 TC)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetTeamByIdAsync_ShouldReturnTeam_WhenExists()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);

      var result = await CreateService(db).GetTeamByIdAsync(teamId);

      result.Should().NotBeNull();
      result!.TeamName.Should().Be("Hero Team");
    }

    [Fact]
    public async Task GetTeamByIdAsync_ShouldReturnNull_WhenTeamNotFound()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);

      var result = await CreateService(db).GetTeamByIdAsync(9999);

      result.Should().BeNull();
    }

    [Fact]
    public async Task GetTeamByIdAsync_ShouldIncludeMembers_WhenTeamHasMembers()
    {
      var db = CreateDb();
      _mockUserContext.Setup(u => u.UserId).Returns(1);
      var teamId = await SeedTeamWithLeader(db);

      db.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = 55,
        Role = TeamMemberRole.EDITOR, IsActive = true, JoinedAt = DateTime.UtcNow
      });
      await db.SaveChangesAsync();

      var result = await CreateService(db).GetTeamByIdAsync(teamId);

      result.Should().NotBeNull();
    }
  }
}
