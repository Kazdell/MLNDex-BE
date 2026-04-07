using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.User;
using Application.Services.User;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.UserServices;

public class UserService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public UserService_CsvAlignedTests(ITestOutputHelper output)
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

  private static async Task SeedUserServiceBaseAsync(MlndexDbContext db)
  {
    db.Roles.AddRange(
      new Role { RoleId = 1, RoleName = RoleName.READER },
      new Role { RoleId = 2, RoleName = RoleName.CREATOR });

    db.Users.AddRange(
      new User
      {
        UserId = 1,
        Username = "user_one",
        Email = "user1@test.com",
        DisplayName = "User One",
        PasswordHash = "hash",
        Bio = "bio1",
        DisplayAvatar = "https://img/avatar1.jpg",
        BannerUrl = "https://img/banner1.jpg",
        NotificationSettings = "{\"email\":true}",
        PrivacySettings = "{\"publicProfile\":true}",
        AppearanceSettings = "{\"theme\":\"dark\"}",
        IsActive = true,
        IsEmailVerified = true,
        CannotUpload = false,
        CreatedAt = DateTime.UtcNow.AddDays(-40)
      },
      new User
      {
        UserId = 2,
        Username = "user_two",
        Email = "user2@test.com",
        DisplayName = "Another User",
        PasswordHash = "hash",
        IsActive = false,
        IsEmailVerified = true,
        CreatedAt = DateTime.UtcNow.AddDays(-15)
      },
      new User
      {
        UserId = 100,
        Username = "creator_owner",
        Email = "creator@test.com",
        DisplayName = "Creator Owner",
        PasswordHash = "hash",
        IsActive = true,
        IsEmailVerified = true,
        CreatedAt = DateTime.UtcNow.AddDays(-100)
      });

    db.UserRoles.AddRange(
      new UserRole { UserRoleId = 10, UserId = 1, RoleId = 1, AssignedAt = DateTime.UtcNow.AddDays(-30) },
      new UserRole { UserRoleId = 11, UserId = 1, RoleId = 2, AssignedAt = DateTime.UtcNow.AddDays(-25) },
      new UserRole { UserRoleId = 12, UserId = 2, RoleId = 1, AssignedAt = DateTime.UtcNow.AddDays(-10) });

    db.Wallets.Add(new Wallet
    {
      WalletId = 1,
      UserId = 1,
      CoinBalance = 1250,
      TotalEarned = 3000,
      TotalSpent = 1750
    });

    db.CreatorProfiles.Add(new CreatorProfile
    {
      CreatorId = 200,
      UserId = 1,
      PenName = "PenUserOne",
      IsActive = true,
      ModerationStatus = ModerationStatus.APPROVED
    });

    db.CreatorProfiles.Add(new CreatorProfile
    {
      CreatorId = 300,
      UserId = 100,
      PenName = "PenCreatorOwner",
      IsActive = true,
      ModerationStatus = ModerationStatus.APPROVED
    });

    db.Series.AddRange(
      new Series
      {
        SeriesId = 10,
        CreatorId = 200,
        Title = "Series A",
        Status = SeriesStatus.ONGOING,
        ModerationStatus = ModerationStatus.APPROVED,
        CreatedAt = DateTime.UtcNow.AddDays(-20)
      },
      new Series
      {
        SeriesId = 11,
        CreatorId = 200,
        Title = "Series B",
        Status = SeriesStatus.ONGOING,
        ModerationStatus = ModerationStatus.APPROVED,
        CreatedAt = DateTime.UtcNow.AddDays(-15)
      });

    db.Chapters.AddRange(
      new Chapter
      {
        ChapterId = 1000,
        SeriesId = 10,
        ChapterNumber = 1,
        Title = "A1",
        ContentType = ContentType.TEXT,
        LockStatus = ChapterLockStatus.FREE,
        Status = ChapterStatus.PUBLISHED,
        ModerationStatus = ModerationStatus.APPROVED,
        CreatedAt = DateTime.UtcNow.AddDays(-10)
      },
      new Chapter
      {
        ChapterId = 1100,
        SeriesId = 11,
        ChapterNumber = 1,
        Title = "B1",
        ContentType = ContentType.TEXT,
        LockStatus = ChapterLockStatus.FREE,
        Status = ChapterStatus.PUBLISHED,
        ModerationStatus = ModerationStatus.APPROVED,
        CreatedAt = DateTime.UtcNow.AddDays(-9)
      });

    db.ReadingHistories.AddRange(
      new ReadingHistory
      {
        HistoryId = 100,
        UserId = 1,
        SeriesId = 10,
        LastChapterId = 1000,
        LastPageNumber = 12,
        LastReadAt = DateTime.UtcNow.AddDays(-2)
      },
      new ReadingHistory
      {
        HistoryId = 101,
        UserId = 1,
        SeriesId = 11,
        LastChapterId = 1100,
        LastPageNumber = 7,
        LastReadAt = DateTime.UtcNow.AddDays(-1)
      });

    db.Follows.AddRange(
      new Follow
      {
        FollowId = 2000,
        UserId = 2,
        TargetId = 200,
        TargetType = FollowTargetType.CREATOR,
        FollowedAt = DateTime.UtcNow.AddDays(-3)
      },
      new Follow
      {
        FollowId = 2001,
        UserId = 1,
        TargetId = 10,
        TargetType = FollowTargetType.SERIES,
        FollowedAt = DateTime.UtcNow.AddDays(-1)
      });

    db.VipPlans.AddRange(
      new VipPlan
      {
        PlanId = 1,
        Name = "Silver",
        Description = "Silver Plan",
        PriceVnd = 99000,
        DurationDays = 30,
        AutoUnlockChapter = true,
        IsActive = true
      },
      new VipPlan
      {
        PlanId = 2,
        Name = "Gold",
        Description = "Gold Plan",
        PriceVnd = 199000,
        DurationDays = 30,
        AutoUnlockChapter = true,
        IsActive = true
      },
      new VipPlan
      {
        PlanId = 3,
        Name = "Old",
        Description = "Old Plan",
        PriceVnd = 50000,
        DurationDays = 15,
        AutoUnlockChapter = false,
        IsActive = false
      });

    db.VipSubscriptions.Add(new VipSubscription
    {
      SubscriptionId = 1,
      UserId = 1,
      PlanId = 2,
      StartDate = DateTime.UtcNow.AddDays(-5),
      EndDate = DateTime.UtcNow.AddDays(25),
      PricePaid = 199000,
      AutoRenew = true,
      Status = SubscriptionStatus.ACTIVE
    });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task GetProfileAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.GetProfileAsync(1, CancellationToken.None);

    _output.WriteLine("Input: userId=1");
    _output.WriteLine($"Output: username={output?.Username}, roles={string.Join(',', output?.Roles ?? new())}, wallet={output?.WalletBalance}, sub={output?.SubscriptionType}");

    output.Should().NotBeNull();
    output!.Username.Should().Be("user_one");
    output.Email.Should().Be("user1@test.com");
    output.Roles.Should().Contain(new[] { "READER", "CREATOR" });
    output.TotalReadSeries.Should().Be(2);
    output.TotalReadChapters.Should().Be(2);
    output.TotalCreatedSeries.Should().Be(2);
    output.FollowersCount.Should().Be(1);
    output.FollowingCount.Should().Be(1);
    output.WalletBalance.Should().Be(1250);
    output.SubscriptionType.Should().Be("Gold");
  }

  [Fact]
  public async Task GetProfileAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.GetProfileAsync(999, CancellationToken.None);

    _output.WriteLine("Input: userId=999");
    _output.WriteLine($"Output: {(output is null ? "null" : "non-null")}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task GetProfileAsync_TC03_BusinessRule_UsesBasicSubscriptionWhenNoActiveSubscription()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var subscription = await db.VipSubscriptions.FirstAsync(x => x.UserId == 1);
    subscription.Status = SubscriptionStatus.EXPIRED;
    subscription.EndDate = DateTime.UtcNow.AddDays(-1);
    await db.SaveChangesAsync();

    var output = await service.GetProfileAsync(1, CancellationToken.None);

    output.Should().NotBeNull();
    output!.SubscriptionType.Should().Be("Cơ bản");
  }

  [Fact]
  public async Task UpdateProfileAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var input = new UpdateProfileDto
    {
      DisplayName = "Updated Name",
      Bio = "Updated bio",
      Avatar = "https://img/new-avatar.jpg",
      BannerUrl = "https://img/new-banner.jpg"
    };

    var output = await service.UpdateProfileAsync(1, input, CancellationToken.None);

    _output.WriteLine("Input: update displayName/bio/avatar/banner for userId=1");
    _output.WriteLine($"Output: updated={output}");

    output.Should().BeTrue();

    var user = await db.Users.FirstAsync(u => u.UserId == 1);
    user.DisplayName.Should().Be("Updated Name");
    user.Bio.Should().Be("Updated bio");
    user.DisplayAvatar.Should().Be("https://img/new-avatar.jpg");
    user.BannerUrl.Should().Be("https://img/new-banner.jpg");
  }

  [Fact]
  public async Task UpdateProfileAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.UpdateProfileAsync(999, new UpdateProfileDto { DisplayName = "x" }, CancellationToken.None);

    _output.WriteLine("Input: update missing userId=999");
    _output.WriteLine($"Output: updated={output}");

    output.Should().BeFalse();
  }

  [Fact]
  public async Task UpdateProfileAsync_TC03_BusinessRule_AllowsPartialUpdate()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.UpdateProfileAsync(1, new UpdateProfileDto { DisplayName = "Only Name" }, CancellationToken.None);

    output.Should().BeTrue();
    var user = await db.Users.FirstAsync(u => u.UserId == 1);
    user.DisplayName.Should().Be("Only Name");
    user.Bio.Should().Be("bio1");
  }

  [Fact]
  public async Task GetReadingHistoryAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.GetReadingHistoryAsync(1, CancellationToken.None);

    _output.WriteLine("Input: userId=1");
    _output.WriteLine($"Output: count={output.Count}, firstSeries={output.FirstOrDefault()?.SeriesId}");

    output.Should().HaveCount(2);
    output[0].SeriesId.Should().Be(11);
    output[0].LastChapterTitle.Should().Be("B1");
    output[1].SeriesId.Should().Be(10);
  }

  [Fact]
  public async Task GetReadingHistoryAsync_TC02_Empty()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.GetReadingHistoryAsync(2, CancellationToken.None);

    _output.WriteLine("Input: userId=2 (no reading history)");
    _output.WriteLine($"Output: count={output.Count}");

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetReadingHistoryAsync_TC03_BusinessRule_OrderedByLastReadDesc()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.GetReadingHistoryAsync(1, CancellationToken.None);

    output.Should().HaveCount(2);
    output[0].LastReadAt.Should().BeAfter(output[1].LastReadAt);
  }

  [Fact]
  public async Task GetVipPlansAsync_TC01_Success_FilterActiveAndOrderPrice()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.GetVipPlansAsync(CancellationToken.None);

    _output.WriteLine("Input: get active vip plans");
    _output.WriteLine($"Output: count={output.Count}, first={output.FirstOrDefault()?.Name}, firstPrice={output.FirstOrDefault()?.PriceVnd}");

    output.Should().HaveCount(2);
    output[0].Name.Should().Be("Silver");
    output[1].Name.Should().Be("Gold");
    output.Any(p => p.Name == "Old").Should().BeFalse();
  }

  [Fact]
  public async Task GetVipPlansAsync_TC02_Empty_WhenNoActivePlans()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    foreach (var plan in db.VipPlans)
    {
      plan.IsActive = false;
    }
    await db.SaveChangesAsync();

    var output = await service.GetVipPlansAsync(CancellationToken.None);

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetVipPlansAsync_TC03_BusinessRule_ReturnsNewlyActivatedPlan()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var oldPlan = await db.VipPlans.FirstAsync(x => x.PlanId == 3);
    oldPlan.IsActive = true;
    await db.SaveChangesAsync();

    var output = await service.GetVipPlansAsync(CancellationToken.None);

    output.Should().HaveCount(3);
    output.Any(x => x.PlanId == 3).Should().BeTrue();
  }

  [Fact]
  public async Task SearchUsersAsync_TC01_Success_WithQuery()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.SearchUsersAsync("user", CancellationToken.None);

    _output.WriteLine("Input: query='user'");
    _output.WriteLine($"Output: count={output.Count}, usernames={string.Join(',', output.Select(x => x.Username))}");

    output.Should().NotBeEmpty();
    output.Any(x => x.Username == "user_one").Should().BeTrue();
    output.Any(x => x.Username == "user_two").Should().BeTrue();
    output.First(x => x.Username == "user_two").IsActive.Should().BeFalse();
  }

  [Fact]
  public async Task SearchUsersAsync_TC02_EmptyQuery_ReturnTop20()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.SearchUsersAsync("", CancellationToken.None);

    _output.WriteLine("Input: query empty");
    _output.WriteLine($"Output: count={output.Count}");

    output.Count.Should().BeLessThanOrEqualTo(20);
    output.Count.Should().BeGreaterThan(0);
  }

  [Fact]
  public async Task SearchUsersAsync_TC03_Success_SearchByDisplayName()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.SearchUsersAsync("Another", CancellationToken.None);

    output.Should().ContainSingle(x => x.Username == "user_two");
  }

  [Fact]
  public async Task GetPublicProfileAsync_TC01_Success_HideSensitiveFields()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.GetPublicProfileAsync("user_one", CancellationToken.None);

    _output.WriteLine("Input: username=user_one");
    _output.WriteLine($"Output: email='{output?.Email}', wallet={output?.WalletBalance}, followers={output?.FollowersCount}");

    output.Should().NotBeNull();
    output!.Username.Should().Be("user_one");
    output.Email.Should().Be(string.Empty);
    output.WalletBalance.Should().Be(0);
    output.FollowersCount.Should().Be(1);
    output.SubscriptionType.Should().Be("Gold");
  }

  [Fact]
  public async Task GetPublicProfileAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.GetPublicProfileAsync("missing_user", CancellationToken.None);

    _output.WriteLine("Input: username=missing_user");
    _output.WriteLine($"Output: {(output is null ? "null" : "non-null")}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task GetPublicProfileAsync_TC03_Success_UserWithoutCreatorProfileHasZeroFollowers()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.GetPublicProfileAsync("user_two", CancellationToken.None);

    output.Should().NotBeNull();
    output!.FollowersCount.Should().Be(0);
    output.FollowingCount.Should().Be(1);
    output.Email.Should().Be(string.Empty);
  }

  [Fact]
  public async Task GetUserSettingsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.GetUserSettingsAsync(1, CancellationToken.None);

    _output.WriteLine("Input: userId=1");
    _output.WriteLine($"Output: notification={output?.NotificationSettings}, privacy={output?.PrivacySettings}, appearance={output?.AppearanceSettings}");

    output.Should().NotBeNull();
    output!.NotificationSettings.Should().Be("{\"email\":true}");
    output.PrivacySettings.Should().Be("{\"publicProfile\":true}");
    output.AppearanceSettings.Should().Be("{\"theme\":\"dark\"}");
  }

  [Fact]
  public async Task GetUserSettingsAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.GetUserSettingsAsync(999, CancellationToken.None);

    _output.WriteLine("Input: userId=999");
    _output.WriteLine($"Output: {(output is null ? "null" : "non-null")}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task GetUserSettingsAsync_TC03_Success_WhenSettingsAreNull()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var user = await db.Users.FirstAsync(u => u.UserId == 1);
    user.NotificationSettings = null;
    user.PrivacySettings = null;
    user.AppearanceSettings = null;
    await db.SaveChangesAsync();

    var output = await service.GetUserSettingsAsync(1, CancellationToken.None);

    output.Should().NotBeNull();
    output!.NotificationSettings.Should().BeNull();
    output.PrivacySettings.Should().BeNull();
    output.AppearanceSettings.Should().BeNull();
  }

  [Fact]
  public async Task UpdateUserSettingsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var input = new UserSettingsDto
    {
      NotificationSettings = "{\"email\":false}",
      PrivacySettings = "{\"publicProfile\":false}",
      AppearanceSettings = "{\"theme\":\"light\"}"
    };

    var output = await service.UpdateUserSettingsAsync(1, input, CancellationToken.None);

    _output.WriteLine("Input: update settings userId=1");
    _output.WriteLine($"Output: updated={output}");

    output.Should().BeTrue();

    var user = await db.Users.FirstAsync(u => u.UserId == 1);
    user.NotificationSettings.Should().Be("{\"email\":false}");
    user.PrivacySettings.Should().Be("{\"publicProfile\":false}");
    user.AppearanceSettings.Should().Be("{\"theme\":\"light\"}");
  }

  [Fact]
  public async Task UpdateUserSettingsAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.UpdateUserSettingsAsync(999, new UserSettingsDto { NotificationSettings = "x" }, CancellationToken.None);

    _output.WriteLine("Input: update settings missing userId=999");
    _output.WriteLine($"Output: updated={output}");

    output.Should().BeFalse();
  }

  [Fact]
  public async Task UpdateUserSettingsAsync_TC03_Success_AllowNullSettings()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserServiceBaseAsync(db);
    var service = new UserService(db);

    var output = await service.UpdateUserSettingsAsync(1, new UserSettingsDto
    {
      NotificationSettings = null,
      PrivacySettings = null,
      AppearanceSettings = null
    }, CancellationToken.None);

    output.Should().BeTrue();
    var user = await db.Users.FirstAsync(u => u.UserId == 1);
    user.NotificationSettings.Should().BeNull();
    user.PrivacySettings.Should().BeNull();
    user.AppearanceSettings.Should().BeNull();
  }
}
