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
				e.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
				e.Property(x => x.DisplayAvatar).HasColumnType("nvarchar(MAX)");
				e.Property(x => x.BannerUrl).HasColumnType("nvarchar(MAX)");
				e.Property(x => x.Bio).HasColumnType("nvarchar(MAX)");
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

            // ====================================================
            // COIN_PACKAGE
            // ====================================================
            modelBuilder.Entity<CoinPackage>(e =>
            {
                e.ToTable("CoinPackage");
                e.HasKey(x => x.PackageId);
                e.Property(x => x.PackageId).UseIdentityColumn();
                e.Property(x => x.Name).HasMaxLength(150).IsRequired();
                e.Property(x => x.CoinAmount).HasColumnType("decimal(10,2)").IsRequired();
                e.Property(x => x.PriceVnd).HasColumnType("decimal(10,2)").IsRequired();
                e.Property(x => x.BonusCoins).HasColumnType("decimal(10,2)").IsRequired();
                e.Property(x => x.IsActive).IsRequired();
                e.Property(x => x.CreatedAt).IsRequired();
            });

            // ====================================================
            // CHAPTER_UNLOCK
            // ====================================================
            modelBuilder.Entity<ChapterUnlock>(e =>
            {
                e.ToTable("ChapterUnlock");
                e.HasKey(x => x.UnlockId);
                e.Property(x => x.UnlockId).UseIdentityColumn();
                e.Property(x => x.CoinsPaid).HasColumnType("decimal(10,2)").IsRequired();
                e.Property(x => x.UnlockSource).HasConversion<string>().IsRequired();

                e.HasOne(x => x.Chapter)
                    .WithMany(c => c.ChapterUnlocks)
                    .HasForeignKey(x => x.ChapterId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Transaction)
                    .WithOne(t => t.ChapterUnlock)
                    .HasForeignKey<ChapterUnlock>(x => x.TransactionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // WITHDRAWAL_REQUEST
            // ====================================================
            modelBuilder.Entity<WithdrawalRequest>(e =>
            {
                e.ToTable("WithdrawalRequest");
                e.HasKey(x => x.WithdrawalId);
                e.Property(x => x.WithdrawalId).UseIdentityColumn();
                e.Property(x => x.AmountCoins).HasColumnType("decimal(10,2)").IsRequired();
                e.Property(x => x.AmountVnd).HasColumnType("decimal(10,2)").IsRequired();
                e.Property(x => x.BankAccountInfo).HasColumnType("nvarchar(MAX)").IsRequired();
                e.Property(x => x.RequestedAt).IsRequired();
                e.Property(x => x.ProcessedAt);
                e.Property(x => x.Status).HasConversion<string>().IsRequired();
                e.Property(x => x.Note).HasColumnType("nvarchar(MAX)");

                e.HasOne(x => x.Creator)
                    .WithMany(c => c.WithdrawalRequests)
                    .HasForeignKey(x => x.CreatorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // READING_HISTORY
            // ====================================================
            modelBuilder.Entity<ReadingHistory>(e =>
            {
                e.ToTable("ReadingHistory");
                e.HasKey(x => x.HistoryId);
                e.Property(x => x.HistoryId).UseIdentityColumn();
                // Unique per user-series
                e.HasIndex(x => new { x.UserId, x.SeriesId }).IsUnique();
                e.Property(x => x.LastPageNumber).IsRequired();
                e.Property(x => x.LastReadAt).IsRequired();

                e.HasOne(x => x.User)
                    .WithMany(u => u.ReadingHistories)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Series)
                    .WithMany(s => s.ReadingHistories)
                    .HasForeignKey(x => x.SeriesId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.LastChapter)
                    .WithMany(c => c.ReadingHistories)
                    .HasForeignKey(x => x.LastChapterId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // FOLLOW
            // ====================================================
            modelBuilder.Entity<Follow>(e =>
            {
                e.ToTable("Follow");
                e.HasKey(x => x.FollowId);
                e.Property(x => x.FollowId).UseIdentityColumn();
                e.Property(x => x.TargetId).IsRequired();
                e.Property(x => x.TargetType).HasConversion<string>().IsRequired();
                e.Property(x => x.FollowedAt).IsRequired();

                e.HasOne(x => x.User)
                    .WithMany(u => u.Follows)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // COMMENT
            // ====================================================
            modelBuilder.Entity<Comment>(e =>
            {
                e.ToTable("Comment");
                e.HasKey(x => x.CommentId);
                e.Property(x => x.CommentId).UseIdentityColumn();
                e.Property(x => x.TargetId).IsRequired();
                e.Property(x => x.TargetType).HasConversion<string>().IsRequired();
                e.Property(x => x.Content).HasColumnType("nvarchar(MAX)").IsRequired();
                e.Property(x => x.IsDeleted).IsRequired();
                e.Property(x => x.CreatedAt).IsRequired();
                e.Property(x => x.UpdatedAt).IsRequired();

                e.HasOne(x => x.User)
                    .WithMany(u => u.Comments)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.ParentComment)
                    .WithMany(c => c.Replies)
                    .HasForeignKey(x => x.ParentCommentId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);
            });

            // ====================================================
            // LIKE
            // ====================================================
            modelBuilder.Entity<Like>(e =>
            {
                e.ToTable("Like");
                e.HasKey(x => x.LikeId);
                e.Property(x => x.LikeId).UseIdentityColumn();
                e.HasIndex(x => new
                    {
                        x.UserId,
                        x.TargetId,
                        x.TargetType,
                    })
                    .IsUnique();
                e.Property(x => x.TargetId).IsRequired();
                e.Property(x => x.TargetType).HasConversion<string>().IsRequired();
                e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()").IsRequired();

                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // BOOKMARK
            // ====================================================
            modelBuilder.Entity<Bookmark>(e =>
            {
                e.ToTable("Bookmark");
                e.HasKey(x => x.BookmarkId);
                e.Property(x => x.BookmarkId).UseIdentityColumn();
                e.Property(x => x.BookmarkedAt).IsRequired();
                e.Property(x => x.Note).HasMaxLength(25);

                e.HasOne(x => x.User)
                    .WithMany(u => u.Bookmarks)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Series)
                    .WithMany(s => s.Bookmarks)
                    .HasForeignKey(x => x.SeriesId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Chapter)
                    .WithMany(c => c.Bookmarks)
                    .HasForeignKey(x => x.ChapterId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);
            });

            // ====================================================
            // RATING
            // ====================================================
            modelBuilder.Entity<Rating>(e =>
            {
                e.ToTable("Rating");
                e.HasKey(x => x.RatingId);
                e.Property(x => x.RatingId).UseIdentityColumn();
                // 1 user chỉ rate 1 series 1 lần
                e.HasIndex(x => new { x.UserId, x.SeriesId }).IsUnique();
                e.Property(x => x.Score).IsRequired();
                e.ToTable(tb =>
                    tb.HasCheckConstraint("CK_Rating_Score", "[Score] >= 1 AND [Score] <= 10")
                );
                e.Property(x => x.Review).HasMaxLength(255);
                e.Property(x => x.CreatedAt).IsRequired();
                e.Property(x => x.UpdatedAt).IsRequired();

                e.HasOne(x => x.User)
                    .WithMany(u => u.Ratings)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Series)
                    .WithMany(s => s.Ratings)
                    .HasForeignKey(x => x.SeriesId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // NOTIFICATION
            // ====================================================
            modelBuilder.Entity<Notification>(e =>
            {
                e.ToTable("Notification");
                e.HasKey(x => x.NotificationId);
                e.Property(x => x.NotificationId).UseIdentityColumn();
                e.Property(x => x.NotificationType).HasConversion<string>().IsRequired();
                e.Property(x => x.Title).HasMaxLength(50).IsRequired();
                e.Property(x => x.Message).HasMaxLength(255).IsRequired();
                e.Property(x => x.ActionUrl).HasMaxLength(2048).IsRequired();
                e.Property(x => x.RelatedEntityId);
                e.Property(x => x.RelatedEntityType).HasColumnType("nvarchar(100)");
                e.Property(x => x.IsRead).IsRequired();
                e.Property(x => x.CreatedAt).IsRequired();

                e.HasOne(x => x.User)
                    .WithMany(u => u.Notifications)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // REPORT
            // ====================================================
            modelBuilder.Entity<Report>(e =>
            {
                e.ToTable("Report");
                e.HasKey(x => x.ReportId);
                e.Property(x => x.ReportId).UseIdentityColumn();
                e.Property(x => x.ContentId).IsRequired();
                e.Property(x => x.ContentType).HasConversion<string>().IsRequired();
                e.Property(x => x.Reason).HasConversion<string>().IsRequired();
                e.Property(x => x.Description).HasMaxLength(255);
                e.Property(x => x.EvidenceUrlsJson).HasColumnType("nvarchar(MAX)");
                e.Property(x => x.Status).HasConversion<string>().IsRequired().HasDefaultValue(ReportStatus.Pending);
                e.Property(x => x.CreatedAt).IsRequired();

                e.HasOne(x => x.Reporter)
                    .WithMany(u => u.Reports)
                    .HasForeignKey(x => x.ReporterId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Queue)
                    .WithMany(q => q.Reports)
                    .HasForeignKey(x => x.QueueId)
                    .OnDelete(DeleteBehavior.Restrict);
            });


            // ====================================================
            // MODERATION_QUEUE
            // ====================================================
            modelBuilder.Entity<ModerationQueue>(e =>
            {
                e.ToTable("ModerationQueue");
                e.HasKey(x => x.QueueId);
                e.Property(x => x.QueueId).UseIdentityColumn();
                e.Property(x => x.ContentId).IsRequired();
                e.Property(x => x.ContentType).HasConversion<string>().IsRequired();
                e.Property(x => x.Priority).HasConversion<string>().IsRequired();
                e.Property(x => x.Status).HasConversion<string>().IsRequired();
                e.Property(x => x.ReportCount).IsRequired();
                e.Property(x => x.FlaggedAt).IsRequired();
                e.Property(x => x.AssignedAt);


                // ── Appeal fields ───────────────────────────────
                e.Property(x => x.AppealReason).HasMaxLength(500);
                e.Property(x => x.AppealCount).IsRequired().HasDefaultValue(0);

                e.HasOne(x => x.AssignedModerator)
                    .WithMany(u => u.AssignedQueues)
                    .HasForeignKey(x => x.AssignedTo)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);
            });

            // ====================================================
            // MODERATION_ACTION
            // ====================================================
            modelBuilder.Entity<ModerationAction>(e =>
            {
                e.ToTable("ModerationAction");
                e.HasKey(x => x.ActionId);
                e.Property(x => x.ActionId).UseIdentityColumn();
                e.Property(x => x.Action).HasConversion<string>().IsRequired();
                e.Property(x => x.Reason).HasColumnType("nvarchar(MAX)").IsRequired();
                e.Property(x => x.ActedAt).IsRequired();

                e.HasOne(x => x.Queue)
                    .WithMany(q => q.ModerationActions)
                    .HasForeignKey(x => x.QueueId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Moderator)
                    .WithMany(u => u.ModerationActions)
                    .HasForeignKey(x => x.ModeratorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================================================
            // TEAM_GENRE
            // ====================================================
            modelBuilder.Entity<TeamGenre>(e =>
            {
                e.ToTable("TeamGenre");
                e.HasKey(x => x.TeamGenreId);
                e.Property(x => x.TeamGenreId).UseIdentityColumn();

                e.HasOne(x => x.TranslationTeam)
                    .WithMany(t => t.TeamGenres)
                    .HasForeignKey(x => x.TeamId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Genre)
                    .WithMany()
                    .HasForeignKey(x => x.GenreId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

			// ====================================================
			// LANGUAGE
			// ====================================================
			modelBuilder.Entity<Language>(e =>
			{
				e.ToTable("Language");
				e.HasKey(x => x.LanguageId);
				e.Property(x => x.LanguageId).UseIdentityColumn();
				e.HasIndex(x => x.Code).IsUnique();
				e.HasIndex(x => x.Name).IsUnique();
				e.Property(x => x.Code).HasMaxLength(10).IsRequired();
				e.Property(x => x.Name).HasMaxLength(50).IsRequired();
			});

			// ====================================================
			// USER_LIST
			// ====================================================
			modelBuilder.Entity<UserList>(e =>
			{
				e.ToTable("UserList");
				e.HasKey(x => x.UserListId);
				e.Property(x => x.UserListId).UseIdentityColumn();
				e.Property(x => x.Name).HasMaxLength(100).IsRequired();
				e.Property(x => x.Description).HasMaxLength(500);
				e.Property(x => x.IsPublic).IsRequired().HasDefaultValue(false);
				e.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("GETUTCDATE()");
				e.Property(x => x.UpdatedAt).IsRequired().HasDefaultValueSql("GETUTCDATE()");

				e.HasOne(x => x.User)
					.WithMany()
					.HasForeignKey(x => x.UserId)
					.OnDelete(DeleteBehavior.Restrict);
			});

			// ====================================================
			// USER_LIST_ITEM
			// ====================================================
			modelBuilder.Entity<UserListItem>(e =>
			{
				e.ToTable("UserListItem");
				e.HasKey(x => x.UserListItemId);
				e.Property(x => x.UserListItemId).UseIdentityColumn();
				e.HasIndex(x => new { x.UserListId, x.SeriesId }).IsUnique();
				e.Property(x => x.AddedAt).IsRequired().HasDefaultValueSql("GETUTCDATE()");

				e.HasOne(x => x.UserList)
					.WithMany(l => l.Items)
					.HasForeignKey(x => x.UserListId)
					.OnDelete(DeleteBehavior.Cascade);

				e.HasOne(x => x.Series)
					.WithMany()
					.HasForeignKey(x => x.SeriesId)
					.OnDelete(DeleteBehavior.Restrict);
			});

			// ====================================================
			// TRUST_SCORE_HISTORY
			// ====================================================
			modelBuilder.Entity<TrustScoreHistory>(e =>
			{
				e.ToTable("TrustScoreHistories");
				e.HasKey(x => x.Id);
				e.Property(x => x.Id).UseIdentityColumn();

				e.Property(x => x.Reason).HasMaxLength(500);
				e.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("GETUTCDATE()");

				e.HasOne(x => x.User)
					.WithMany(u => u.TrustScoreHistories)
					.HasForeignKey(x => x.UserId)
					.OnDelete(DeleteBehavior.Cascade);

				e.HasOne(x => x.TranslationTeam)
					.WithMany(t => t.TrustScoreHistories)
					.HasForeignKey(x => x.TranslationTeamId)
					.OnDelete(DeleteBehavior.Cascade);

				e.HasOne(x => x.RelatedReport)
					.WithMany()
					.HasForeignKey(x => x.RelatedReportId)
					.OnDelete(DeleteBehavior.SetNull);
			});

			modelBuilder.Entity<Appeal>(e =>
			{
				e.ToTable("Appeals");
				e.HasKey(x => x.AppealId);
				e.Property(x => x.AppealId).UseIdentityColumn();

				e.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
				e.Property(x => x.EvidenceUrl).HasMaxLength(500);
				e.Property(x => x.ReviewNotes).HasMaxLength(2000);
				e.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("GETUTCDATE()");

				e.HasOne(x => x.User)
					.WithMany()
					.HasForeignKey(x => x.UserId)
					.OnDelete(DeleteBehavior.Restrict);

				e.HasOne(x => x.Reviewer)
					.WithMany()
					.HasForeignKey(x => x.ReviewedBy)
					.OnDelete(DeleteBehavior.Restrict);

				e.HasOne(x => x.RelatedReport)
					.WithMany()
					.HasForeignKey(x => x.RelatedReportId)
					.OnDelete(DeleteBehavior.SetNull);
			});

			// ====================================================
			// SYSTEM_SETTING
			// ====================================================
			modelBuilder.Entity<SystemSetting>(e =>
			{
				e.ToTable("SystemSetting");
				e.HasKey(x => x.SettingId);
				e.Property(x => x.SettingId).UseIdentityColumn();
				e.HasIndex(x => x.Key).IsUnique();
				e.Property(x => x.Key).HasMaxLength(100).IsRequired();
				e.Property(x => x.Value).IsRequired();
				e.Property(x => x.UpdatedAt).IsRequired().HasDefaultValueSql("GETUTCDATE()");
			});
        }
    }
}
