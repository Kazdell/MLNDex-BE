using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Interfaces.Data
{
    public interface IMlndexDbContext
    {
        DbSet<Domain.Entities.User> Users { get; set; }
        DbSet<Role> Roles { get; set; }
        DbSet<UserRole> UserRoles { get; set; }
        DbSet<CreatorProfile> CreatorProfiles { get; set; }
        DbSet<VipPlan> VipPlans { get; set; }
        DbSet<VipSubscription> VipSubscriptions { get; set; }
        DbSet<TranslationTeam> TranslationTeams { get; set; }
        DbSet<TeamMember> TeamMembers { get; set; }
        DbSet<TeamGenre> TeamGenres { get; set; }
        DbSet<Domain.Entities.Series> Series { get; set; }
        DbSet<Genre> Genres { get; set; }
        DbSet<SeriesGenre> SeriesGenres { get; set; }
        DbSet<Domain.Entities.Chapter> Chapters { get; set; }
        DbSet<ChapterPage> ChapterPages { get; set; }
        DbSet<ChapterText> ChapterTexts { get; set; }
        DbSet<TranslationPermission> TranslationPermissions { get; set; }
        DbSet<Domain.Entities.Language> Languages { get; set; }
        DbSet<Domain.Entities.Translation> Translations { get; set; }
        DbSet<TranslationPage> TranslationPages { get; set; }
        DbSet<TranslationText> TranslationTexts { get; set; }
        DbSet<Wallet> Wallets { get; set; }
        DbSet<Transaction> Transactions { get; set; }
        DbSet<CoinPackage> CoinPackages { get; set; }
        DbSet<ChapterUnlock> ChapterUnlocks { get; set; }
        DbSet<WithdrawalRequest> WithdrawalRequests { get; set; }
        DbSet<ReadingHistory> ReadingHistories { get; set; }
        DbSet<Follow> Follows { get; set; }
        DbSet<Comment> Comments { get; set; }
        DbSet<Bookmark> Bookmarks { get; set; }
        DbSet<Rating> Ratings { get; set; }
        DbSet<Like> Likes { get; set; }
        DbSet<Domain.Entities.Notification> Notifications { get; set; }
        DbSet<Report> Reports { get; set; }
        DbSet<ModerationQueue> ModerationQueues { get; set; }
        DbSet<ModerationAction> ModerationActions { get; set; }
        DbSet<TeamInvitation> TeamInvitations { get; set; }
        DbSet<TeamJoinRequest> TeamJoinRequests { get; set; }
        DbSet<CoinRateSetting> CoinRateSettings { get; set; }

		Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
        DbSet<UserList> UserLists { get; set; }
        DbSet<UserListItem> UserListItems { get; set; }
        DbSet<TrustScoreHistory> TrustScoreHistories { get; set; }
        DbSet<TranslationCredit> TranslationCredits { get; set; }
        DbSet<Appeal> Appeals { get; set; }
        DbSet<SystemSetting> SystemSettings { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
  }
}
