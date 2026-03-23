using Application.Interfaces.Data;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Data
{
    public class MlndexDbContext : DbContext, IMlndexDbContext
    {
        public MlndexDbContext(DbContextOptions<MlndexDbContext> options) : base(options) { }

        // ==================== USER MANAGEMENT ====================
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }

        // ==================== CREATOR ====================
        public DbSet<CreatorProfile> CreatorProfiles { get; set; }

        // ==================== VIP ====================
        public DbSet<VipPlan> VipPlans { get; set; }
        public DbSet<VipSubscription> VipSubscriptions { get; set; }

        // ==================== TRANSLATION TEAM ====================
        public DbSet<TranslationTeam> TranslationTeams { get; set; }
        public DbSet<TeamMember> TeamMembers { get; set; }
        public DbSet<TeamGenre> TeamGenres { get; set; }

        // ==================== CONTENT ====================
        public DbSet<Series> Series { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<SeriesGenre> SeriesGenres { get; set; }
        public DbSet<Chapter> Chapters { get; set; }
        public DbSet<ChapterPage> ChapterPages { get; set; }
        public DbSet<ChapterText> ChapterTexts { get; set; }

        // ==================== TRANSLATION ====================
        public DbSet<TranslationPermission> TranslationPermissions { get; set; }
        public DbSet<TeamInvitation> TeamInvitations { get; set; }
        public DbSet<TeamJoinRequest> TeamJoinRequests { get; set; }
        public DbSet<Translation> Translations { get; set; }
        public DbSet<TranslationPage> TranslationPages { get; set; }
        public DbSet<TranslationText> TranslationTexts { get; set; }
        public DbSet<Language> Languages { get; set; }
        public DbSet<TranslationCredit> TranslationCredits { get; set; }
        public DbSet<TranslationTeamJoin> TranslationTeamJoins { get; set; }

        // ==================== FINANCIAL ====================
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<CoinPackage> CoinPackages { get; set; }
        public DbSet<ChapterUnlock> ChapterUnlocks { get; set; }
        public DbSet<WithdrawalRequest> WithdrawalRequests { get; set; }
        public DbSet<CoinRateSetting> CoinRateSettings { get; set; }

        // ==================== INTERACTION ====================
        public DbSet<ReadingHistory> ReadingHistories { get; set; }
        public DbSet<Follow> Follows { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Bookmark> Bookmarks { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<Like> Likes { get; set; }

        // ==================== NOTIFICATION ====================
        public DbSet<Notification> Notifications { get; set; }

        // ==================== REPORT & MODERATION ====================
        public DbSet<Report> Reports { get; set; }
        public DbSet<ModerationQueue> ModerationQueues { get; set; }
        public DbSet<ModerationAction> ModerationActions { get; set; }
        public DbSet<TrustScoreHistory> TrustScoreHistories { get; set; }
        public DbSet<Appeal> Appeals { get; set; }

        // ==================== LISTS ====================
        public DbSet<UserList> UserLists { get; set; }
        public DbSet<UserListItem> UserListItems { get; set; }

        // ==================== SYSTEM ====================
        public DbSet<SystemSetting> SystemSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ====================================================
            // USER
            // ====================================================
            modelBuilder.Entity<User>(e =>
            {
                e.ToTable("User");
                e.HasKey(x => x.UserId);
                e.Property(x => x.UserId).UseIdentityColumn();
                e.HasIndex(x => x.Username).IsUnique();
                e.HasIndex(x => x.Email).IsUnique();
                e.Property(x => x.Username).HasMaxLength(25).IsRequired();
                e.Property(x => x.Email).HasMaxLength(256).IsRequired();
                e.Property(x => x.PasswordHash).IsRequired(false); // Từ nhánh Auth
                e.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
                e.Property(x => x.DisplayAvatar).HasColumnType("nvarchar(MAX)");
                e.Property(x => x.BannerUrl).HasColumnType("nvarchar(MAX)"); // Từ nhánh main
                e.Property(x => x.Bio).HasColumnType("nvarchar(MAX)"); // Từ nhánh main
                e.Property(x => x.IsActive).IsRequired();
            });

            // ====================================================
            // ROLE
            // ====================================================
            modelBuilder.Entity<Role>(e =>
            {
                e.ToTable("Role");
                e.HasKey(x => x.RoleId);
                e.Property(x => x.RoleId).UseIdentityColumn();
                e.Property(x => x.RoleName)
                 .HasMaxLength(15)
                 .HasConversion<string>()
                 .IsRequired();
            });

            // ====================================================
            // USER_ROLE
            // ====================================================
            modelBuilder.Entity<UserRole>(e =>
            {
                e.ToTable("UserRole");
                e.HasKey(x => x.UserRoleId);
                e.Property(x => x.UserRoleId).UseIdentityColumn();
                e.Property(x => x.AssignedAt).IsRequired();

                e.HasOne(x => x.User)
                 .WithMany(u => u.UserRoles)
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Role)
                 .WithMany(r => r.UserRoles)
                 .HasForeignKey(x => x.RoleId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // CREATOR_PROFILE
            // ====================================================
            modelBuilder.Entity<CreatorProfile>(e =>
            {
                e.ToTable("CreatorProfile");
                e.HasKey(x => x.CreatorId);
                e.Property(x => x.CreatorId).UseIdentityColumn();
                e.Property(x => x.PenName).HasMaxLength(25).IsRequired();
                e.Property(x => x.ReputationScore).IsRequired();
                e.Property(x => x.TotalRevenue).HasColumnType("decimal(10,2)").IsRequired();
                e.Property(x => x.HideRevenue).IsRequired();
                e.Property(x => x.IsActive).IsRequired();
                e.Property(x => x.ModerationStatus)
                 .HasConversion<string>()
                 .IsRequired();

                e.HasOne(x => x.User)
                 .WithOne(u => u.CreatorProfile)
                 .HasForeignKey<CreatorProfile>(x => x.UserId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // VIP_PLAN
            // ====================================================
            modelBuilder.Entity<VipPlan>(e =>
            {
                e.ToTable("VipPlan");
                e.HasKey(x => x.PlanId);
                e.Property(x => x.PlanId).UseIdentityColumn();
                e.Property(x => x.Name).HasMaxLength(25).IsRequired();
                e.Property(x => x.Description).HasMaxLength(255);
                e.Property(x => x.PriceVnd).HasColumnType("decimal(10,2)").IsRequired();
                e.Property(x => x.DurationDays).IsRequired();
                e.Property(x => x.AutoUnlockChapter).IsRequired();
                e.Property(x => x.IsActive).IsRequired();
            });

            // ====================================================
            // TRANSLATION_PERMISSION
            // ====================================================
            modelBuilder.Entity<TranslationPermission>(e =>
            {
                e.ToTable("TranslationPermission");
                e.HasKey(x => x.PermissionId);
                e.Property(x => x.PermissionId).UseIdentityColumn();
                e.Property(x => x.Status)
                 .HasConversion<string>()
                 .HasMaxLength(20)
                 .IsRequired();
                e.Property(x => x.Origin)
                 .HasConversion<string>()
                 .HasMaxLength(30)
                 .IsRequired()
                 .HasDefaultValue(PermissionOrigin.REQUESTED_BY_TEAM);
                e.Property(x => x.LanguageId).IsRequired().HasDefaultValue(1);
                e.Property(x => x.GrantedAt);
                e.Property(x => x.RevokedAt);
                e.Property(x => x.Note).HasMaxLength(255);

                // Unique constraint per team + series + language
                e.HasIndex(x => new { x.TeamId, x.SeriesId, x.LanguageId }).IsUnique();

                e.HasOne(x => x.Series)
                 .WithMany(s => s.TranslationPermissions)
                 .HasForeignKey(x => x.SeriesId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Team)
                 .WithMany(t => t.TranslationPermissions)
                 .HasForeignKey(x => x.TeamId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.GrantedByUser)
                 .WithMany(u => u.GrantedPermissions)
                 .HasForeignKey(x => x.GrantedBy)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Language)
                 .WithMany(l => l.TranslationPermissions)
                 .HasForeignKey(x => x.LanguageId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // TEAM_INVITATION
            // ====================================================
            modelBuilder.Entity<TeamInvitation>(e =>
            {
                e.ToTable("TeamInvitation");
                e.HasKey(x => x.InvitationId);
                e.Property(x => x.InvitationId).UseIdentityColumn();
                e.Property(x => x.Status).HasConversion<string>().IsRequired();
                e.Property(x => x.Role).HasMaxLength(20).IsRequired();

                e.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId);
                e.HasOne(x => x.Invitee).WithMany().HasForeignKey(x => x.InviteeId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Inviter).WithMany().HasForeignKey(x => x.InviterId).OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // TEAM_JOIN_REQUEST
            // ====================================================
            modelBuilder.Entity<TeamJoinRequest>(e =>
            {
                e.ToTable("TeamJoinRequest");
                e.HasKey(x => x.RequestId);
                e.Property(x => x.RequestId).UseIdentityColumn();
                e.Property(x => x.Status).HasConversion<string>().IsRequired();
                e.Property(x => x.Message).HasMaxLength(500);

                e.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.RespondedByUser).WithMany().HasForeignKey(x => x.RespondedBy).OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // VIP_SUBSCRIPTION
            // ====================================================
            modelBuilder.Entity<VipSubscription>(e =>
            {
                e.ToTable("VipSubscription");
                e.HasKey(x => x.SubscriptionId);
                e.Property(x => x.SubscriptionId).UseIdentityColumn();
                e.Property(x => x.StartDate).IsRequired();
                e.Property(x => x.EndDate).IsRequired();
                e.Property(x => x.PricePaid).HasColumnType("decimal(10,2)").IsRequired();
                e.Property(x => x.AutoRenew).IsRequired();
                e.Property(x => x.Status)
                 .HasConversion<string>()
                 .IsRequired();

                e.HasOne(x => x.User)
                 .WithMany(u => u.VipSubscriptions)
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.VipPlan)
                 .WithMany(p => p.VipSubscriptions)
                 .HasForeignKey(x => x.PlanId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // TRANSLATION_TEAM
            // ====================================================
            modelBuilder.Entity<TranslationTeam>(e =>
            {
                e.ToTable("TranslationTeam");
                e.HasKey(x => x.TeamId);
                e.Property(x => x.TeamId).UseIdentityColumn();
                e.HasIndex(x => x.TeamName).IsUnique();
                e.Property(x => x.TeamName).HasMaxLength(140).IsRequired();
                e.Property(x => x.Slug).HasMaxLength(140).IsRequired();
                e.Property(x => x.Description).HasMaxLength(1000);
                e.Property(x => x.LanguageId).IsRequired().HasDefaultValue(1);
                e.Property(x => x.RequireApproval).IsRequired().HasDefaultValue(true);
                e.Property(x => x.ReputationScore).IsRequired();
                e.Property(x => x.LockStatus)
                 .HasConversion<string>()
                 .IsRequired();
                e.Property(x => x.IsMonetizationEnabled).IsRequired();
                e.Property(x => x.LockedAt);
                e.Property(x => x.ModerationStatus)
                 .HasConversion<string>()
                 .IsRequired();
                e.Property(x => x.AvatarUrl).HasColumnType("nvarchar(MAX)");
                e.Property(x => x.BannerUrl).HasColumnType("nvarchar(MAX)");
                e.Property(x => x.Facebook).HasMaxLength(255);
                e.Property(x => x.Discord).HasMaxLength(255);
                e.Property(x => x.Website).HasMaxLength(255);

                e.HasOne(x => x.Leader)
                 .WithMany(u => u.LeadingTeams)
                 .HasForeignKey(x => x.LeaderId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Language)
                 .WithMany(l => l.TranslationTeams)
                 .HasForeignKey(x => x.LanguageId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.LockedByUser)
                 .WithMany(u => u.LockedTeams)
                 .HasForeignKey(x => x.LockedBy)
                 .OnDelete(DeleteBehavior.Restrict)
                 .IsRequired(false);
            });

            // ====================================================
            // TEAM_MEMBER
            // ====================================================
            modelBuilder.Entity<TeamMember>(e =>
            {
                e.ToTable("TeamMember");
                e.HasKey(x => x.MembershipId);
                e.Property(x => x.MembershipId).UseIdentityColumn();
                e.Property(x => x.Role)
                 .HasConversion<string>()
                 .IsRequired();
                e.Property(x => x.JoinedAt).IsRequired();
                e.Property(x => x.IsActive).IsRequired();

                e.HasOne(x => x.TranslationTeam)
                 .WithMany(t => t.TeamMembers)
                 .HasForeignKey(x => x.TeamId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.User)
                 .WithMany(u => u.TeamMemberships)
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // GENRE
            // ====================================================
            modelBuilder.Entity<Genre>(e =>
            {
                e.ToTable("Genre");
                e.HasKey(x => x.GenreId);
                e.Property(x => x.GenreId).UseIdentityColumn();
                e.HasIndex(x => x.Name).IsUnique();
                e.Property(x => x.Name).HasMaxLength(25).IsRequired();
                e.Property(x => x.Description).HasMaxLength(255);
            });

            // ====================================================
            // SERIES
            // ====================================================
            modelBuilder.Entity<Series>(e =>
            {
                e.ToTable("Series");
                e.HasKey(x => x.SeriesId);
                e.Property(x => x.SeriesId).UseIdentityColumn();
                e.HasIndex(x => x.Title).IsUnique();
                e.Property(x => x.Title).HasMaxLength(450).IsRequired();
                e.Property(x => x.Description).HasColumnType("nvarchar(MAX)");
                e.Property(x => x.CoverImageUrl).HasColumnType("nvarchar(MAX)");
                e.Property(x => x.SeriesFormat).HasConversion<string>().IsRequired();
                e.Property(x => x.AgeRating).HasConversion<string>().IsRequired();
                e.Property(x => x.Status).HasConversion<string>().IsRequired();
                e.Property(x => x.ModerationStatus).HasConversion<string>().IsRequired();
                e.Property(x => x.AverageRating).HasColumnType("decimal(10,2)").IsRequired();
                e.Property(x => x.TotalRatings).IsRequired();
                e.Property(x => x.CreatedAt).IsRequired();

                e.HasOne(x => x.Creator)
                 .WithMany(c => c.Series)
                 .HasForeignKey(x => x.CreatorId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // SERIES_GENRE
            // ====================================================
            modelBuilder.Entity<SeriesGenre>(e =>
            {
                e.ToTable("SeriesGenre");
                e.HasKey(x => x.SeriesGenreId);
                e.Property(x => x.SeriesGenreId).UseIdentityColumn();

                e.HasOne(x => x.Series)
                 .WithMany(s => s.SeriesGenres)
                 .HasForeignKey(x => x.SeriesId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Genre)
                 .WithMany(g => g.SeriesGenres)
                 .HasForeignKey(x => x.GenreId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // CHAPTER
            // ====================================================
            modelBuilder.Entity<Chapter>(e =>
            {
                e.ToTable("Chapter");
                e.HasKey(x => x.ChapterId);
                e.Property(x => x.ChapterId).UseIdentityColumn();
                e.Property(x => x.ChapterNumber).IsRequired();
                e.Property(x => x.Title).HasMaxLength(255);
                e.Property(x => x.ContentType).HasConversion<string>().IsRequired();
                e.Property(x => x.PageCount);
                e.Property(x => x.WordCount);
                e.Property(x => x.LockStatus).HasConversion<string>().IsRequired();
                e.Property(x => x.UnlockPriceCoins);
                e.Property(x => x.UnlockTime);
                e.Property(x => x.Status).HasConversion<string>().IsRequired();
                e.Property(x => x.ModerationStatus).HasConversion<string>().IsRequired();
                e.Property(x => x.PublishedAt);
                e.Property(x => x.Views).HasDefaultValue(0).IsRequired();

                e.HasOne(x => x.Series)
                 .WithMany(s => s.Chapters)
                 .HasForeignKey(x => x.SeriesId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Team)
                 .WithMany(t => t.Chapters)
                 .HasForeignKey(x => x.TeamId)
                 .OnDelete(DeleteBehavior.Restrict)
                 .IsRequired(false);

                e.HasOne(x => x.Language)
                 .WithMany(l => l.Chapters)
                 .HasForeignKey(x => x.LanguageId)
                 .OnDelete(DeleteBehavior.Restrict)
                 .IsRequired(false);
            });

            // ====================================================
            // CHAPTER_PAGE
            // ====================================================
            modelBuilder.Entity<ChapterPage>(e =>
            {
                e.ToTable("ChapterPage");
                e.HasKey(x => x.PageId);
                e.Property(x => x.PageId).UseIdentityColumn();
                e.Property(x => x.PageNumber).IsRequired();
                e.Property(x => x.ImageUrl).HasMaxLength(2048).IsRequired();

                e.HasOne(x => x.Chapter)
                 .WithMany(c => c.Pages)
                 .HasForeignKey(x => x.ChapterId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ====================================================
            // CHAPTER_TEXT
            // ====================================================
            modelBuilder.Entity<ChapterText>(e =>
            {
                e.ToTable("ChapterText");
                e.HasKey(x => x.TextId);
                e.Property(x => x.TextId).UseIdentityColumn();
                e.HasIndex(x => x.ChapterId).IsUnique(); // 1-1
                e.Property(x => x.ContentUrl).HasMaxLength(2048).IsRequired();
                e.Property(x => x.WordCount).IsRequired();

                e.HasOne(x => x.Chapter)
                 .WithOne(c => c.ChapterText)
                 .HasForeignKey<ChapterText>(x => x.ChapterId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ====================================================
            // TRANSLATION
            // ====================================================
            modelBuilder.Entity<Translation>(e =>
            {
                e.ToTable("Translation");
                e.HasKey(x => x.TranslationId);
                e.Property(x => x.TranslationId).UseIdentityColumn();
                e.Property(x => x.LanguageId).IsRequired().HasDefaultValue(1);
                e.Property(x => x.ContentType).HasConversion<string>().IsRequired();
                e.Property(x => x.QualityStatus).HasConversion<string>().IsRequired();
                e.Property(x => x.ModerationStatus).HasConversion<string>().IsRequired();
                e.Property(x => x.PublishedAt);

                e.HasOne(x => x.Chapter)
                 .WithMany(c => c.Translations)
                 .HasForeignKey(x => x.ChapterId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Permission)
                 .WithMany(p => p.Translations)
                 .HasForeignKey(x => x.PermissionId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Language)
                 .WithMany(l => l.Translations)
                 .HasForeignKey(x => x.LanguageId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // TRANSLATION_PAGE
            // ====================================================
            modelBuilder.Entity<TranslationPage>(e =>
            {
                e.ToTable("TranslationPage");
                e.HasKey(x => x.TransPageId);
                e.Property(x => x.TransPageId).UseIdentityColumn();
                e.Property(x => x.PageNumber).IsRequired();
                e.Property(x => x.TranslationImageUrl).HasMaxLength(2048).IsRequired();

                e.HasOne(x => x.Translation)
                 .WithMany(t => t.TranslationPages)
                 .HasForeignKey(x => x.TranslationId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ====================================================
            // TRANSLATION_TEXT
            // ====================================================
            modelBuilder.Entity<TranslationText>(e =>
            {
                e.ToTable("TranslationText");
                e.HasKey(x => x.TransTextId);
                e.Property(x => x.TransTextId).UseIdentityColumn();
                e.HasIndex(x => x.TranslationId).IsUnique(); // 1-1
                e.Property(x => x.ContentUrl).HasMaxLength(2048).IsRequired();
                e.Property(x => x.WordCount).IsRequired();

                e.HasOne(x => x.Translation)
                 .WithOne(t => t.TranslationText)
                 .HasForeignKey<TranslationText>(x => x.TranslationId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ====================================================
            // TRANSLATION_CREDIT
            // ====================================================
            modelBuilder.Entity<TranslationCredit>(e =>
            {
                e.ToTable("TranslationCredit");
                e.HasKey(x => new { x.TranslationId, x.UserId, x.Role });

                e.HasOne(x => x.Translation)
                 .WithMany(t => t.TranslationCredits)
                 .HasForeignKey(x => x.TranslationId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.User)
                 .WithMany(u => u.TranslationCredits)
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // TRANSLATION_TEAM_JOIN
            // ====================================================
            modelBuilder.Entity<TranslationTeamJoin>(e =>
            {
                e.ToTable("TranslationTeamJoin");
                e.HasKey(x => new { x.TranslationId, x.TeamId });

                e.HasOne(x => x.Translation)
                 .WithMany(t => t.TeamJoins)
                 .HasForeignKey(x => x.TranslationId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Team)
                 .WithMany(t => t.TeamJoins)
                 .HasForeignKey(x => x.TeamId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // WALLET
            // ====================================================
            modelBuilder.Entity<Wallet>(e =>
            {
                e.ToTable("Wallet");
                e.HasKey(x => x.WalletId);
                e.Property(x => x.WalletId).UseIdentityColumn();
                e.HasIndex(x => x.UserId).IsUnique(); // 1-1
                e.Property(x => x.CoinBalance).HasColumnType("decimal(10,2)").IsRequired();
                e.Property(x => x.TotalEarned).HasColumnType("decimal(10,2)").IsRequired();
                e.Property(x => x.TotalSpent).HasColumnType("decimal(10,2)").IsRequired();

                // CoinBalance >= 0
                e.ToTable(tb =>
                 tb.HasCheckConstraint("CK_Wallet_CoinBalance", "[CoinBalance] >= 0")
                );

                e.HasOne(x => x.User)
                 .WithOne(u => u.Wallet)
                 .HasForeignKey<Wallet>(x => x.UserId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // TRANSACTION
            // ====================================================
            modelBuilder.Entity<Transaction>(e =>
            {
                e.ToTable("Transaction");
                e.HasKey(x => x.TransactionId);
                e.Property(x => x.TransactionId).UseIdentityColumn();
                e.Property(x => x.Type).HasConversion<string>().IsRequired();
                e.Property(x => x.AmountCoins).HasColumnType("decimal(10,2)").IsRequired();
                e.Property(x => x.RelatedEntityId);
                e.Property(x => x.RelatedEntityType).HasColumnType("nvarchar(100)");
                e.Property(x => x.Status).HasConversion<string>().IsRequired();
                e.Property(x => x.Note).HasMaxLength(150);
                e.Property(x => x.CreatedAt).IsRequired();

                e.HasOne(x => x.User)
                 .WithMany(u => u.Transactions)
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Wallet)
                 .WithMany(w => w.Transactions)
                 .HasForeignKey(x => x.WalletId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // =================================================