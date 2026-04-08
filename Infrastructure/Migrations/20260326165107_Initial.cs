using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
  /// <inheritdoc />
  public partial class Initial : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
          name: "CoinPackage",
          columns: table => new
          {
            PackageId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
            CoinAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
            PriceCoins = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
            BonusCoins = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
            IsActive = table.Column<bool>(type: "bit", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_CoinPackage", x => x.PackageId);
          });

      migrationBuilder.CreateTable(
          name: "Genre",
          columns: table => new
          {
            GenreId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            Name = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
            Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Genre", x => x.GenreId);
          });

      migrationBuilder.CreateTable(
          name: "Language",
          columns: table => new
          {
            LanguageId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
            Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Language", x => x.LanguageId);
          });

      migrationBuilder.CreateTable(
          name: "Role",
          columns: table => new
          {
            RoleId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            RoleName = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Role", x => x.RoleId);
          });

      migrationBuilder.CreateTable(
          name: "SystemConfigs",
          columns: table => new
          {
            Id = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            ExchangeRateCoinToVnd = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
            WithdrawalFeePercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
            WithdrawalMinCoins = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
            WithdrawalMaxCoins = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
            BlacklistWordsJson = table.Column<string>(type: "nvarchar(MAX)", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_SystemConfigs", x => x.Id);
          });

      migrationBuilder.CreateTable(
          name: "User",
          columns: table => new
          {
            UserId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            Username = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
            Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
            DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
            DisplayAvatar = table.Column<string>(type: "nvarchar(MAX)", nullable: true),
            Bio = table.Column<string>(type: "nvarchar(MAX)", nullable: true),
            PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
            IsEmailVerified = table.Column<bool>(type: "bit", nullable: false),
            GoogleId = table.Column<string>(type: "nvarchar(max)", nullable: true),
            FacebookId = table.Column<string>(type: "nvarchar(max)", nullable: true),
            IsActive = table.Column<bool>(type: "bit", nullable: false),
            TrustScore = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
            CannotUpload = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            BannerUrl = table.Column<string>(type: "nvarchar(MAX)", nullable: true),
            NotificationSettings = table.Column<string>(type: "nvarchar(max)", nullable: true),
            PrivacySettings = table.Column<string>(type: "nvarchar(max)", nullable: true),
            AppearanceSettings = table.Column<string>(type: "nvarchar(max)", nullable: true),
            RefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
            RefreshTokenExpiryTime = table.Column<DateTime>(type: "datetime2", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_User", x => x.UserId);
          });

      migrationBuilder.CreateTable(
          name: "VipPlan",
          columns: table => new
          {
            PlanId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            Name = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
            Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
            PriceCoins = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
            DurationDays = table.Column<int>(type: "int", nullable: false),
            AutoUnlockChapter = table.Column<bool>(type: "bit", nullable: false),
            IsActive = table.Column<bool>(type: "bit", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_VipPlan", x => x.PlanId);
          });

      migrationBuilder.CreateTable(
          name: "CoinRateSetting",
          columns: table => new
          {
            Id = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            CoinsPerVnd = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
            MinTopUpVnd = table.Column<long>(type: "bigint", nullable: false),
            MaxTopUpVnd = table.Column<long>(type: "bigint", nullable: false),
            IsActive = table.Column<bool>(type: "bit", nullable: false),
            UpdatedByUserId = table.Column<int>(type: "int", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_CoinRateSetting", x => x.Id);
            table.ForeignKey(
                      name: "FK_CoinRateSetting_User_UpdatedByUserId",
                      column: x => x.UpdatedByUserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "Comment",
          columns: table => new
          {
            CommentId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<int>(type: "int", nullable: false),
            TargetId = table.Column<int>(type: "int", nullable: false),
            TargetType = table.Column<string>(type: "nvarchar(max)", nullable: false),
            Content = table.Column<string>(type: "nvarchar(MAX)", nullable: false),
            ParentCommentId = table.Column<int>(type: "int", nullable: true),
            IsDeleted = table.Column<bool>(type: "bit", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Comment", x => x.CommentId);
            table.ForeignKey(
                      name: "FK_Comment_Comment_ParentCommentId",
                      column: x => x.ParentCommentId,
                      principalTable: "Comment",
                      principalColumn: "CommentId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_Comment_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "CreatorProfile",
          columns: table => new
          {
            CreatorId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<int>(type: "int", nullable: false),
            PenName = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
            ReputationScore = table.Column<int>(type: "int", nullable: false),
            TotalRevenue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
            HideRevenue = table.Column<bool>(type: "bit", nullable: false),
            IsActive = table.Column<bool>(type: "bit", nullable: false),
            ModerationStatus = table.Column<string>(type: "nvarchar(max)", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_CreatorProfile", x => x.CreatorId);
            table.ForeignKey(
                      name: "FK_CreatorProfile_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "Follow",
          columns: table => new
          {
            FollowId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<int>(type: "int", nullable: false),
            TargetId = table.Column<int>(type: "int", nullable: false),
            TargetType = table.Column<string>(type: "nvarchar(max)", nullable: false),
            FollowedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Follow", x => x.FollowId);
            table.ForeignKey(
                      name: "FK_Follow_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "Like",
          columns: table => new
          {
            LikeId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<int>(type: "int", nullable: false),
            TargetId = table.Column<int>(type: "int", nullable: false),
            TargetType = table.Column<string>(type: "nvarchar(450)", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Like", x => x.LikeId);
            table.ForeignKey(
                      name: "FK_Like_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "ModerationQueue",
          columns: table => new
          {
            QueueId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            ContentId = table.Column<int>(type: "int", nullable: false),
            ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
            Priority = table.Column<string>(type: "nvarchar(max)", nullable: false),
            AssignedTo = table.Column<int>(type: "int", nullable: true),
            Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
            ReportCount = table.Column<int>(type: "int", nullable: false),
            FlaggedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            RetryCount = table.Column<int>(type: "int", nullable: false),
            LastRetryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            AppealReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
            AppealCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_ModerationQueue", x => x.QueueId);
            table.ForeignKey(
                      name: "FK_ModerationQueue_User_AssignedTo",
                      column: x => x.AssignedTo,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "Notification",
          columns: table => new
          {
            NotificationId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<int>(type: "int", nullable: false),
            NotificationType = table.Column<string>(type: "nvarchar(max)", nullable: false),
            Title = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
            Message = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
            ActionUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
            RelatedEntityId = table.Column<int>(type: "int", nullable: true),
            RelatedEntityType = table.Column<string>(type: "nvarchar(100)", nullable: true),
            IsRead = table.Column<bool>(type: "bit", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Notification", x => x.NotificationId);
            table.ForeignKey(
                      name: "FK_Notification_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "TranslationTeam",
          columns: table => new
          {
            TeamId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            LeaderId = table.Column<int>(type: "int", nullable: false),
            TeamName = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
            Slug = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
            Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
            LanguageId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
            RequireApproval = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
            ReputationScore = table.Column<int>(type: "int", nullable: false),
            TrustScore = table.Column<int>(type: "int", nullable: false),
            LockStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
            ModerationStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
            IsMonetizationEnabled = table.Column<bool>(type: "bit", nullable: false),
            AvatarUrl = table.Column<string>(type: "nvarchar(MAX)", nullable: true),
            BannerUrl = table.Column<string>(type: "nvarchar(MAX)", nullable: true),
            Facebook = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
            Discord = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
            Website = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
            Certificates = table.Column<string>(type: "nvarchar(max)", nullable: true),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            LockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            LockedBy = table.Column<int>(type: "int", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_TranslationTeam", x => x.TeamId);
            table.ForeignKey(
                      name: "FK_TranslationTeam_Language_LanguageId",
                      column: x => x.LanguageId,
                      principalTable: "Language",
                      principalColumn: "LanguageId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_TranslationTeam_User_LeaderId",
                      column: x => x.LeaderId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_TranslationTeam_User_LockedBy",
                      column: x => x.LockedBy,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "UserList",
          columns: table => new
          {
            UserListId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<int>(type: "int", nullable: false),
            Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
            Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
            UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
            IsPublic = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_UserList", x => x.UserListId);
            table.ForeignKey(
                      name: "FK_UserList_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "UserRole",
          columns: table => new
          {
            UserRoleId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<int>(type: "int", nullable: false),
            RoleId = table.Column<int>(type: "int", nullable: false),
            AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_UserRole", x => x.UserRoleId);
            table.ForeignKey(
                      name: "FK_UserRole_Role_RoleId",
                      column: x => x.RoleId,
                      principalTable: "Role",
                      principalColumn: "RoleId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_UserRole_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "Wallet",
          columns: table => new
          {
            WalletId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<int>(type: "int", nullable: false),
            CoinBalance = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
            TotalEarned = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
            TotalSpent = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Wallet", x => x.WalletId);
            table.CheckConstraint("CK_Wallet_CoinBalance", "[CoinBalance] >= 0");
            table.ForeignKey(
                      name: "FK_Wallet_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "VipSubscription",
          columns: table => new
          {
            SubscriptionId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<int>(type: "int", nullable: false),
            PlanId = table.Column<int>(type: "int", nullable: false),
            StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
            EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
            PricePaid = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
            AutoRenew = table.Column<bool>(type: "bit", nullable: false),
            Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_VipSubscription", x => x.SubscriptionId);
            table.ForeignKey(
                      name: "FK_VipSubscription_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_VipSubscription_VipPlan_PlanId",
                      column: x => x.PlanId,
                      principalTable: "VipPlan",
                      principalColumn: "PlanId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "Series",
          columns: table => new
          {
            SeriesId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            CreatorId = table.Column<int>(type: "int", nullable: false),
            Title = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
            Description = table.Column<string>(type: "nvarchar(MAX)", nullable: true),
            CoverImageUrl = table.Column<string>(type: "nvarchar(MAX)", nullable: true),
            SeriesFormat = table.Column<string>(type: "nvarchar(max)", nullable: false),
            AgeRating = table.Column<string>(type: "nvarchar(max)", nullable: false),
            Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
            ModerationStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
            ViolenceScore = table.Column<int>(type: "int", nullable: false),
            NudityScore = table.Column<int>(type: "int", nullable: false),
            SexualScore = table.Column<int>(type: "int", nullable: false),
            LanguageScore = table.Column<int>(type: "int", nullable: false),
            SubstancesScore = table.Column<int>(type: "int", nullable: false),
            SensitiveScore = table.Column<int>(type: "int", nullable: false),
            AverageRating = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
            TotalRatings = table.Column<int>(type: "int", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Series", x => x.SeriesId);
            table.ForeignKey(
                      name: "FK_Series_CreatorProfile_CreatorId",
                      column: x => x.CreatorId,
                      principalTable: "CreatorProfile",
                      principalColumn: "CreatorId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "WithdrawalRequest",
          columns: table => new
          {
            WithdrawalId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            CreatorId = table.Column<int>(type: "int", nullable: false),
            AmountCoins = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
            AmountVnd = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
            BankAccountInfo = table.Column<string>(type: "nvarchar(MAX)", nullable: false),
            RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
            Note = table.Column<string>(type: "nvarchar(MAX)", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_WithdrawalRequest", x => x.WithdrawalId);
            table.ForeignKey(
                      name: "FK_WithdrawalRequest_CreatorProfile_CreatorId",
                      column: x => x.CreatorId,
                      principalTable: "CreatorProfile",
                      principalColumn: "CreatorId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "ModerationAction",
          columns: table => new
          {
            ActionId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            QueueId = table.Column<int>(type: "int", nullable: false),
            ModeratorId = table.Column<int>(type: "int", nullable: false),
            Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
            Reason = table.Column<string>(type: "nvarchar(MAX)", nullable: false),
            ActedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_ModerationAction", x => x.ActionId);
            table.ForeignKey(
                      name: "FK_ModerationAction_ModerationQueue_QueueId",
                      column: x => x.QueueId,
                      principalTable: "ModerationQueue",
                      principalColumn: "QueueId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_ModerationAction_User_ModeratorId",
                      column: x => x.ModeratorId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "Report",
          columns: table => new
          {
            ReportId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            ReporterId = table.Column<int>(type: "int", nullable: false),
            ContentId = table.Column<int>(type: "int", nullable: false),
            ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
            Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
            Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
            EvidenceUrlsJson = table.Column<string>(type: "nvarchar(MAX)", nullable: false),
            Status = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "Pending"),
            QueueId = table.Column<int>(type: "int", nullable: true),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Report", x => x.ReportId);
            table.ForeignKey(
                      name: "FK_Report_ModerationQueue_QueueId",
                      column: x => x.QueueId,
                      principalTable: "ModerationQueue",
                      principalColumn: "QueueId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_Report_User_ReporterId",
                      column: x => x.ReporterId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "TeamGenre",
          columns: table => new
          {
            TeamGenreId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            TeamId = table.Column<int>(type: "int", nullable: false),
            GenreId = table.Column<int>(type: "int", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_TeamGenre", x => x.TeamGenreId);
            table.ForeignKey(
                      name: "FK_TeamGenre_Genre_GenreId",
                      column: x => x.GenreId,
                      principalTable: "Genre",
                      principalColumn: "GenreId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_TeamGenre_TranslationTeam_TeamId",
                      column: x => x.TeamId,
                      principalTable: "TranslationTeam",
                      principalColumn: "TeamId",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "TeamInvitation",
          columns: table => new
          {
            InvitationId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            TeamId = table.Column<int>(type: "int", nullable: false),
            InviteeId = table.Column<int>(type: "int", nullable: false),
            InviterId = table.Column<int>(type: "int", nullable: false),
            Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
            Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_TeamInvitation", x => x.InvitationId);
            table.ForeignKey(
                      name: "FK_TeamInvitation_TranslationTeam_TeamId",
                      column: x => x.TeamId,
                      principalTable: "TranslationTeam",
                      principalColumn: "TeamId",
                      onDelete: ReferentialAction.Cascade);
            table.ForeignKey(
                      name: "FK_TeamInvitation_User_InviteeId",
                      column: x => x.InviteeId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_TeamInvitation_User_InviterId",
                      column: x => x.InviterId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "TeamJoinRequest",
          columns: table => new
          {
            RequestId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            TeamId = table.Column<int>(type: "int", nullable: false),
            UserId = table.Column<int>(type: "int", nullable: false),
            Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
            Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            RespondedBy = table.Column<int>(type: "int", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_TeamJoinRequest", x => x.RequestId);
            table.ForeignKey(
                      name: "FK_TeamJoinRequest_TranslationTeam_TeamId",
                      column: x => x.TeamId,
                      principalTable: "TranslationTeam",
                      principalColumn: "TeamId",
                      onDelete: ReferentialAction.Cascade);
            table.ForeignKey(
                      name: "FK_TeamJoinRequest_User_RespondedBy",
                      column: x => x.RespondedBy,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_TeamJoinRequest_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "TeamMember",
          columns: table => new
          {
            MembershipId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            TeamId = table.Column<int>(type: "int", nullable: false),
            UserId = table.Column<int>(type: "int", nullable: false),
            Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
            JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            IsActive = table.Column<bool>(type: "bit", nullable: false),
            LeftAt = table.Column<DateTime>(type: "datetime2", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_TeamMember", x => x.MembershipId);
            table.ForeignKey(
                      name: "FK_TeamMember_TranslationTeam_TeamId",
                      column: x => x.TeamId,
                      principalTable: "TranslationTeam",
                      principalColumn: "TeamId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_TeamMember_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "Transaction",
          columns: table => new
          {
            TransactionId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<int>(type: "int", nullable: false),
            WalletId = table.Column<int>(type: "int", nullable: false),
            Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
            AmountCoins = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
            RelatedEntityId = table.Column<int>(type: "int", nullable: true),
            RelatedEntityType = table.Column<string>(type: "nvarchar(100)", nullable: true),
            Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
            Note = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Transaction", x => x.TransactionId);
            table.ForeignKey(
                      name: "FK_Transaction_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_Transaction_Wallet_WalletId",
                      column: x => x.WalletId,
                      principalTable: "Wallet",
                      principalColumn: "WalletId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "Chapter",
          columns: table => new
          {
            ChapterId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            SeriesId = table.Column<int>(type: "int", nullable: false),
            TeamId = table.Column<int>(type: "int", nullable: true),
            LanguageId = table.Column<int>(type: "int", nullable: true),
            ChapterNumber = table.Column<float>(type: "real", nullable: false),
            Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
            ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
            PageCount = table.Column<int>(type: "int", nullable: true),
            WordCount = table.Column<int>(type: "int", nullable: true),
            LockStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
            UnlockPriceCoins = table.Column<int>(type: "int", nullable: true),
            UnlockTime = table.Column<DateTime>(type: "datetime2", nullable: true),
            Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
            ModerationStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
            AiScoresJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
            PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            Views = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Chapter", x => x.ChapterId);
            table.ForeignKey(
                      name: "FK_Chapter_Language_LanguageId",
                      column: x => x.LanguageId,
                      principalTable: "Language",
                      principalColumn: "LanguageId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_Chapter_Series_SeriesId",
                      column: x => x.SeriesId,
                      principalTable: "Series",
                      principalColumn: "SeriesId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_Chapter_TranslationTeam_TeamId",
                      column: x => x.TeamId,
                      principalTable: "TranslationTeam",
                      principalColumn: "TeamId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "Rating",
          columns: table => new
          {
            RatingId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<int>(type: "int", nullable: false),
            SeriesId = table.Column<int>(type: "int", nullable: false),
            Score = table.Column<int>(type: "int", nullable: false),
            Review = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Rating", x => x.RatingId);
            table.CheckConstraint("CK_Rating_Score", "[Score] >= 1 AND [Score] <= 10");
            table.ForeignKey(
                      name: "FK_Rating_Series_SeriesId",
                      column: x => x.SeriesId,
                      principalTable: "Series",
                      principalColumn: "SeriesId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_Rating_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "SeriesGenre",
          columns: table => new
          {
            SeriesGenreId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            SeriesId = table.Column<int>(type: "int", nullable: false),
            GenreId = table.Column<int>(type: "int", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_SeriesGenre", x => x.SeriesGenreId);
            table.ForeignKey(
                      name: "FK_SeriesGenre_Genre_GenreId",
                      column: x => x.GenreId,
                      principalTable: "Genre",
                      principalColumn: "GenreId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_SeriesGenre_Series_SeriesId",
                      column: x => x.SeriesId,
                      principalTable: "Series",
                      principalColumn: "SeriesId",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "TranslationPermission",
          columns: table => new
          {
            PermissionId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            SeriesId = table.Column<int>(type: "int", nullable: false),
            TeamId = table.Column<int>(type: "int", nullable: false),
            GrantedBy = table.Column<int>(type: "int", nullable: false),
            LanguageId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
            Origin = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "REQUESTED_BY_TEAM"),
            Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            Note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_TranslationPermission", x => x.PermissionId);
            table.ForeignKey(
                      name: "FK_TranslationPermission_Language_LanguageId",
                      column: x => x.LanguageId,
                      principalTable: "Language",
                      principalColumn: "LanguageId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_TranslationPermission_Series_SeriesId",
                      column: x => x.SeriesId,
                      principalTable: "Series",
                      principalColumn: "SeriesId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_TranslationPermission_TranslationTeam_TeamId",
                      column: x => x.TeamId,
                      principalTable: "TranslationTeam",
                      principalColumn: "TeamId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_TranslationPermission_User_GrantedBy",
                      column: x => x.GrantedBy,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "UserListItem",
          columns: table => new
          {
            UserListItemId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            UserListId = table.Column<int>(type: "int", nullable: false),
            SeriesId = table.Column<int>(type: "int", nullable: false),
            AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_UserListItem", x => x.UserListItemId);
            table.ForeignKey(
                      name: "FK_UserListItem_Series_SeriesId",
                      column: x => x.SeriesId,
                      principalTable: "Series",
                      principalColumn: "SeriesId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_UserListItem_UserList_UserListId",
                      column: x => x.UserListId,
                      principalTable: "UserList",
                      principalColumn: "UserListId",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "Appeals",
          columns: table => new
          {
            AppealId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<int>(type: "int", nullable: false),
            RelatedReportId = table.Column<int>(type: "int", nullable: true),
            Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
            EvidenceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
            Status = table.Column<int>(type: "int", nullable: false),
            ReviewedBy = table.Column<int>(type: "int", nullable: true),
            ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
            ScoreRestored = table.Column<int>(type: "int", nullable: true),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
            ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Appeals", x => x.AppealId);
            table.ForeignKey(
                      name: "FK_Appeals_Report_RelatedReportId",
                      column: x => x.RelatedReportId,
                      principalTable: "Report",
                      principalColumn: "ReportId",
                      onDelete: ReferentialAction.SetNull);
            table.ForeignKey(
                      name: "FK_Appeals_User_ReviewedBy",
                      column: x => x.ReviewedBy,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_Appeals_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "TrustScoreHistories",
          columns: table => new
          {
            Id = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<int>(type: "int", nullable: true),
            TranslationTeamId = table.Column<int>(type: "int", nullable: true),
            ScoreChange = table.Column<int>(type: "int", nullable: false),
            Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
            RelatedReportId = table.Column<int>(type: "int", nullable: true),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_TrustScoreHistories", x => x.Id);
            table.ForeignKey(
                      name: "FK_TrustScoreHistories_Report_RelatedReportId",
                      column: x => x.RelatedReportId,
                      principalTable: "Report",
                      principalColumn: "ReportId",
                      onDelete: ReferentialAction.SetNull);
            table.ForeignKey(
                      name: "FK_TrustScoreHistories_TranslationTeam_TranslationTeamId",
                      column: x => x.TranslationTeamId,
                      principalTable: "TranslationTeam",
                      principalColumn: "TeamId",
                      onDelete: ReferentialAction.Cascade);
            table.ForeignKey(
                      name: "FK_TrustScoreHistories_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "Bookmark",
          columns: table => new
          {
            BookmarkId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<int>(type: "int", nullable: false),
            SeriesId = table.Column<int>(type: "int", nullable: false),
            ChapterId = table.Column<int>(type: "int", nullable: true),
            BookmarkedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            Note = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Bookmark", x => x.BookmarkId);
            table.ForeignKey(
                      name: "FK_Bookmark_Chapter_ChapterId",
                      column: x => x.ChapterId,
                      principalTable: "Chapter",
                      principalColumn: "ChapterId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_Bookmark_Series_SeriesId",
                      column: x => x.SeriesId,
                      principalTable: "Series",
                      principalColumn: "SeriesId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_Bookmark_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "ChapterPage",
          columns: table => new
          {
            PageId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            ChapterId = table.Column<int>(type: "int", nullable: false),
            PageNumber = table.Column<int>(type: "int", nullable: false),
            ImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_ChapterPage", x => x.PageId);
            table.ForeignKey(
                      name: "FK_ChapterPage_Chapter_ChapterId",
                      column: x => x.ChapterId,
                      principalTable: "Chapter",
                      principalColumn: "ChapterId",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "ChapterText",
          columns: table => new
          {
            TextId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            ChapterId = table.Column<int>(type: "int", nullable: false),
            ContentUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
            WordCount = table.Column<int>(type: "int", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_ChapterText", x => x.TextId);
            table.ForeignKey(
                      name: "FK_ChapterText_Chapter_ChapterId",
                      column: x => x.ChapterId,
                      principalTable: "Chapter",
                      principalColumn: "ChapterId",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "ChapterUnlock",
          columns: table => new
          {
            UnlockId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            ChapterId = table.Column<int>(type: "int", nullable: false),
            UserId = table.Column<int>(type: "int", nullable: false),
            TransactionId = table.Column<int>(type: "int", nullable: false),
            CoinsPaid = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
            UnlockSource = table.Column<string>(type: "nvarchar(max)", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_ChapterUnlock", x => x.UnlockId);
            table.ForeignKey(
                      name: "FK_ChapterUnlock_Chapter_ChapterId",
                      column: x => x.ChapterId,
                      principalTable: "Chapter",
                      principalColumn: "ChapterId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_ChapterUnlock_Transaction_TransactionId",
                      column: x => x.TransactionId,
                      principalTable: "Transaction",
                      principalColumn: "TransactionId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_ChapterUnlock_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "ReadingHistory",
          columns: table => new
          {
            HistoryId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<int>(type: "int", nullable: false),
            SeriesId = table.Column<int>(type: "int", nullable: false),
            LastChapterId = table.Column<int>(type: "int", nullable: false),
            LastPageNumber = table.Column<int>(type: "int", nullable: false),
            LastReadAt = table.Column<DateTime>(type: "datetime2", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_ReadingHistory", x => x.HistoryId);
            table.ForeignKey(
                      name: "FK_ReadingHistory_Chapter_LastChapterId",
                      column: x => x.LastChapterId,
                      principalTable: "Chapter",
                      principalColumn: "ChapterId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_ReadingHistory_Series_SeriesId",
                      column: x => x.SeriesId,
                      principalTable: "Series",
                      principalColumn: "SeriesId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_ReadingHistory_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "Translation",
          columns: table => new
          {
            TranslationId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            ChapterId = table.Column<int>(type: "int", nullable: false),
            PermissionId = table.Column<int>(type: "int", nullable: false),
            LanguageId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
            ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
            QualityStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
            ModerationStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
            PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
            IsOfficial = table.Column<bool>(type: "bit", nullable: false),
            IsOutdated = table.Column<bool>(type: "bit", nullable: false),
            IsOrphan = table.Column<bool>(type: "bit", nullable: false),
            AiScoresJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_Translation", x => x.TranslationId);
            table.ForeignKey(
                      name: "FK_Translation_Chapter_ChapterId",
                      column: x => x.ChapterId,
                      principalTable: "Chapter",
                      principalColumn: "ChapterId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_Translation_Language_LanguageId",
                      column: x => x.LanguageId,
                      principalTable: "Language",
                      principalColumn: "LanguageId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_Translation_TranslationPermission_PermissionId",
                      column: x => x.PermissionId,
                      principalTable: "TranslationPermission",
                      principalColumn: "PermissionId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "TranslationCredit",
          columns: table => new
          {
            TranslationId = table.Column<int>(type: "int", nullable: false),
            UserId = table.Column<int>(type: "int", nullable: false),
            Role = table.Column<int>(type: "int", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_TranslationCredit", x => new { x.TranslationId, x.UserId, x.Role });
            table.ForeignKey(
                      name: "FK_TranslationCredit_Translation_TranslationId",
                      column: x => x.TranslationId,
                      principalTable: "Translation",
                      principalColumn: "TranslationId",
                      onDelete: ReferentialAction.Cascade);
            table.ForeignKey(
                      name: "FK_TranslationCredit_User_UserId",
                      column: x => x.UserId,
                      principalTable: "User",
                      principalColumn: "UserId",
                      onDelete: ReferentialAction.Restrict);
          });

      migrationBuilder.CreateTable(
          name: "TranslationPage",
          columns: table => new
          {
            TransPageId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            TranslationId = table.Column<int>(type: "int", nullable: false),
            PageNumber = table.Column<int>(type: "int", nullable: false),
            TranslationImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_TranslationPage", x => x.TransPageId);
            table.ForeignKey(
                      name: "FK_TranslationPage_Translation_TranslationId",
                      column: x => x.TranslationId,
                      principalTable: "Translation",
                      principalColumn: "TranslationId",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "TranslationTeamJoin",
          columns: table => new
          {
            TranslationId = table.Column<int>(type: "int", nullable: false),
            TeamId = table.Column<int>(type: "int", nullable: false),
            IsPrimary = table.Column<bool>(type: "bit", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_TranslationTeamJoin", x => new { x.TranslationId, x.TeamId });
            table.ForeignKey(
                      name: "FK_TranslationTeamJoin_TranslationTeam_TeamId",
                      column: x => x.TeamId,
                      principalTable: "TranslationTeam",
                      principalColumn: "TeamId",
                      onDelete: ReferentialAction.Restrict);
            table.ForeignKey(
                      name: "FK_TranslationTeamJoin_Translation_TranslationId",
                      column: x => x.TranslationId,
                      principalTable: "Translation",
                      principalColumn: "TranslationId",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateTable(
          name: "TranslationText",
          columns: table => new
          {
            TransTextId = table.Column<int>(type: "int", nullable: false)
                  .Annotation("SqlServer:Identity", "1, 1"),
            TranslationId = table.Column<int>(type: "int", nullable: false),
            ContentUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
            WordCount = table.Column<int>(type: "int", nullable: false)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_TranslationText", x => x.TransTextId);
            table.ForeignKey(
                      name: "FK_TranslationText_Translation_TranslationId",
                      column: x => x.TranslationId,
                      principalTable: "Translation",
                      principalColumn: "TranslationId",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateIndex(
          name: "IX_Appeals_RelatedReportId",
          table: "Appeals",
          column: "RelatedReportId");

      migrationBuilder.CreateIndex(
          name: "IX_Appeals_ReviewedBy",
          table: "Appeals",
          column: "ReviewedBy");

      migrationBuilder.CreateIndex(
          name: "IX_Appeals_UserId",
          table: "Appeals",
          column: "UserId");

      migrationBuilder.CreateIndex(
          name: "IX_Bookmark_ChapterId",
          table: "Bookmark",
          column: "ChapterId");

      migrationBuilder.CreateIndex(
          name: "IX_Bookmark_SeriesId",
          table: "Bookmark",
          column: "SeriesId");

      migrationBuilder.CreateIndex(
          name: "IX_Bookmark_UserId",
          table: "Bookmark",
          column: "UserId");

      migrationBuilder.CreateIndex(
          name: "IX_Chapter_LanguageId",
          table: "Chapter",
          column: "LanguageId");

      migrationBuilder.CreateIndex(
          name: "IX_Chapter_SeriesId",
          table: "Chapter",
          column: "SeriesId");

      migrationBuilder.CreateIndex(
          name: "IX_Chapter_TeamId",
          table: "Chapter",
          column: "TeamId");

      migrationBuilder.CreateIndex(
          name: "IX_ChapterPage_ChapterId",
          table: "ChapterPage",
          column: "ChapterId");

      migrationBuilder.CreateIndex(
          name: "IX_ChapterText_ChapterId",
          table: "ChapterText",
          column: "ChapterId",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_ChapterUnlock_ChapterId",
          table: "ChapterUnlock",
          column: "ChapterId");

      migrationBuilder.CreateIndex(
          name: "IX_ChapterUnlock_TransactionId",
          table: "ChapterUnlock",
          column: "TransactionId",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_ChapterUnlock_UserId",
          table: "ChapterUnlock",
          column: "UserId");

      migrationBuilder.CreateIndex(
          name: "IX_CoinRateSetting_UpdatedByUserId",
          table: "CoinRateSetting",
          column: "UpdatedByUserId");

      migrationBuilder.CreateIndex(
          name: "IX_Comment_ParentCommentId",
          table: "Comment",
          column: "ParentCommentId");

      migrationBuilder.CreateIndex(
          name: "IX_Comment_UserId",
          table: "Comment",
          column: "UserId");

      migrationBuilder.CreateIndex(
          name: "IX_CreatorProfile_UserId",
          table: "CreatorProfile",
          column: "UserId",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_Follow_UserId",
          table: "Follow",
          column: "UserId");

      migrationBuilder.CreateIndex(
          name: "IX_Genre_Name",
          table: "Genre",
          column: "Name",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_Language_Code",
          table: "Language",
          column: "Code",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_Language_Name",
          table: "Language",
          column: "Name",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_Like_UserId_TargetId_TargetType",
          table: "Like",
          columns: new[] { "UserId", "TargetId", "TargetType" },
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_ModerationAction_ModeratorId",
          table: "ModerationAction",
          column: "ModeratorId");

      migrationBuilder.CreateIndex(
          name: "IX_ModerationAction_QueueId",
          table: "ModerationAction",
          column: "QueueId");

      migrationBuilder.CreateIndex(
          name: "IX_ModerationQueue_AssignedTo",
          table: "ModerationQueue",
          column: "AssignedTo");

      migrationBuilder.CreateIndex(
          name: "IX_Notification_UserId",
          table: "Notification",
          column: "UserId");

      migrationBuilder.CreateIndex(
          name: "IX_Rating_SeriesId",
          table: "Rating",
          column: "SeriesId");

      migrationBuilder.CreateIndex(
          name: "IX_Rating_UserId_SeriesId",
          table: "Rating",
          columns: new[] { "UserId", "SeriesId" },
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_ReadingHistory_LastChapterId",
          table: "ReadingHistory",
          column: "LastChapterId");

      migrationBuilder.CreateIndex(
          name: "IX_ReadingHistory_SeriesId",
          table: "ReadingHistory",
          column: "SeriesId");

      migrationBuilder.CreateIndex(
          name: "IX_ReadingHistory_UserId_SeriesId",
          table: "ReadingHistory",
          columns: new[] { "UserId", "SeriesId" },
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_Report_QueueId",
          table: "Report",
          column: "QueueId");

      migrationBuilder.CreateIndex(
          name: "IX_Report_ReporterId",
          table: "Report",
          column: "ReporterId");

      migrationBuilder.CreateIndex(
          name: "IX_Series_CreatorId",
          table: "Series",
          column: "CreatorId");

      migrationBuilder.CreateIndex(
          name: "IX_Series_Title",
          table: "Series",
          column: "Title",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_SeriesGenre_GenreId",
          table: "SeriesGenre",
          column: "GenreId");

      migrationBuilder.CreateIndex(
          name: "IX_SeriesGenre_SeriesId",
          table: "SeriesGenre",
          column: "SeriesId");

      migrationBuilder.CreateIndex(
          name: "IX_TeamGenre_GenreId",
          table: "TeamGenre",
          column: "GenreId");

      migrationBuilder.CreateIndex(
          name: "IX_TeamGenre_TeamId",
          table: "TeamGenre",
          column: "TeamId");

      migrationBuilder.CreateIndex(
          name: "IX_TeamInvitation_InviteeId",
          table: "TeamInvitation",
          column: "InviteeId");

      migrationBuilder.CreateIndex(
          name: "IX_TeamInvitation_InviterId",
          table: "TeamInvitation",
          column: "InviterId");

      migrationBuilder.CreateIndex(
          name: "IX_TeamInvitation_TeamId",
          table: "TeamInvitation",
          column: "TeamId");

      migrationBuilder.CreateIndex(
          name: "IX_TeamJoinRequest_RespondedBy",
          table: "TeamJoinRequest",
          column: "RespondedBy");

      migrationBuilder.CreateIndex(
          name: "IX_TeamJoinRequest_TeamId",
          table: "TeamJoinRequest",
          column: "TeamId");

      migrationBuilder.CreateIndex(
          name: "IX_TeamJoinRequest_UserId",
          table: "TeamJoinRequest",
          column: "UserId");

      migrationBuilder.CreateIndex(
          name: "IX_TeamMember_TeamId",
          table: "TeamMember",
          column: "TeamId");

      migrationBuilder.CreateIndex(
          name: "IX_TeamMember_UserId",
          table: "TeamMember",
          column: "UserId");

      migrationBuilder.CreateIndex(
          name: "IX_Transaction_UserId",
          table: "Transaction",
          column: "UserId");

      migrationBuilder.CreateIndex(
          name: "IX_Transaction_WalletId",
          table: "Transaction",
          column: "WalletId");

      migrationBuilder.CreateIndex(
          name: "IX_Translation_ChapterId",
          table: "Translation",
          column: "ChapterId");

      migrationBuilder.CreateIndex(
          name: "IX_Translation_LanguageId",
          table: "Translation",
          column: "LanguageId");

      migrationBuilder.CreateIndex(
          name: "IX_Translation_PermissionId",
          table: "Translation",
          column: "PermissionId");

      migrationBuilder.CreateIndex(
          name: "IX_TranslationCredit_UserId",
          table: "TranslationCredit",
          column: "UserId");

      migrationBuilder.CreateIndex(
          name: "IX_TranslationPage_TranslationId",
          table: "TranslationPage",
          column: "TranslationId");

      migrationBuilder.CreateIndex(
          name: "IX_TranslationPermission_GrantedBy",
          table: "TranslationPermission",
          column: "GrantedBy");

      migrationBuilder.CreateIndex(
          name: "IX_TranslationPermission_LanguageId",
          table: "TranslationPermission",
          column: "LanguageId");

      migrationBuilder.CreateIndex(
          name: "IX_TranslationPermission_SeriesId",
          table: "TranslationPermission",
          column: "SeriesId");

      migrationBuilder.CreateIndex(
          name: "IX_TranslationPermission_TeamId_SeriesId_LanguageId",
          table: "TranslationPermission",
          columns: new[] { "TeamId", "SeriesId", "LanguageId" },
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_TranslationTeam_LanguageId",
          table: "TranslationTeam",
          column: "LanguageId");

      migrationBuilder.CreateIndex(
          name: "IX_TranslationTeam_LeaderId",
          table: "TranslationTeam",
          column: "LeaderId");

      migrationBuilder.CreateIndex(
          name: "IX_TranslationTeam_LockedBy",
          table: "TranslationTeam",
          column: "LockedBy");

      migrationBuilder.CreateIndex(
          name: "IX_TranslationTeam_TeamName",
          table: "TranslationTeam",
          column: "TeamName",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_TranslationTeamJoin_TeamId",
          table: "TranslationTeamJoin",
          column: "TeamId");

      migrationBuilder.CreateIndex(
          name: "IX_TranslationText_TranslationId",
          table: "TranslationText",
          column: "TranslationId",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_TrustScoreHistories_RelatedReportId",
          table: "TrustScoreHistories",
          column: "RelatedReportId");

      migrationBuilder.CreateIndex(
          name: "IX_TrustScoreHistories_TranslationTeamId",
          table: "TrustScoreHistories",
          column: "TranslationTeamId");

      migrationBuilder.CreateIndex(
          name: "IX_TrustScoreHistories_UserId",
          table: "TrustScoreHistories",
          column: "UserId");

      migrationBuilder.CreateIndex(
          name: "IX_User_Email",
          table: "User",
          column: "Email",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_User_Username",
          table: "User",
          column: "Username",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_UserList_UserId",
          table: "UserList",
          column: "UserId");

      migrationBuilder.CreateIndex(
          name: "IX_UserListItem_SeriesId",
          table: "UserListItem",
          column: "SeriesId");

      migrationBuilder.CreateIndex(
          name: "IX_UserListItem_UserListId_SeriesId",
          table: "UserListItem",
          columns: new[] { "UserListId", "SeriesId" },
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_UserRole_RoleId",
          table: "UserRole",
          column: "RoleId");

      migrationBuilder.CreateIndex(
          name: "IX_UserRole_UserId",
          table: "UserRole",
          column: "UserId");

      migrationBuilder.CreateIndex(
          name: "IX_VipSubscription_PlanId",
          table: "VipSubscription",
          column: "PlanId");

      migrationBuilder.CreateIndex(
          name: "IX_VipSubscription_UserId",
          table: "VipSubscription",
          column: "UserId");

      migrationBuilder.CreateIndex(
          name: "IX_Wallet_UserId",
          table: "Wallet",
          column: "UserId",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_WithdrawalRequest_CreatorId",
          table: "WithdrawalRequest",
          column: "CreatorId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "Appeals");

      migrationBuilder.DropTable(
          name: "Bookmark");

      migrationBuilder.DropTable(
          name: "ChapterPage");

      migrationBuilder.DropTable(
          name: "ChapterText");

      migrationBuilder.DropTable(
          name: "ChapterUnlock");

      migrationBuilder.DropTable(
          name: "CoinPackage");

      migrationBuilder.DropTable(
          name: "CoinRateSetting");

      migrationBuilder.DropTable(
          name: "Comment");

      migrationBuilder.DropTable(
          name: "Follow");

      migrationBuilder.DropTable(
          name: "Like");

      migrationBuilder.DropTable(
          name: "ModerationAction");

      migrationBuilder.DropTable(
          name: "Notification");

      migrationBuilder.DropTable(
          name: "Rating");

      migrationBuilder.DropTable(
          name: "ReadingHistory");

      migrationBuilder.DropTable(
          name: "SeriesGenre");

      migrationBuilder.DropTable(
          name: "SystemConfigs");

      migrationBuilder.DropTable(
          name: "TeamGenre");

      migrationBuilder.DropTable(
          name: "TeamInvitation");

      migrationBuilder.DropTable(
          name: "TeamJoinRequest");

      migrationBuilder.DropTable(
          name: "TeamMember");

      migrationBuilder.DropTable(
          name: "TranslationCredit");

      migrationBuilder.DropTable(
          name: "TranslationPage");

      migrationBuilder.DropTable(
          name: "TranslationTeamJoin");

      migrationBuilder.DropTable(
          name: "TranslationText");

      migrationBuilder.DropTable(
          name: "TrustScoreHistories");

      migrationBuilder.DropTable(
          name: "UserListItem");

      migrationBuilder.DropTable(
          name: "UserRole");

      migrationBuilder.DropTable(
          name: "VipSubscription");

      migrationBuilder.DropTable(
          name: "WithdrawalRequest");

      migrationBuilder.DropTable(
          name: "Transaction");

      migrationBuilder.DropTable(
          name: "Genre");

      migrationBuilder.DropTable(
          name: "Translation");

      migrationBuilder.DropTable(
          name: "Report");

      migrationBuilder.DropTable(
          name: "UserList");

      migrationBuilder.DropTable(
          name: "Role");

      migrationBuilder.DropTable(
          name: "VipPlan");

      migrationBuilder.DropTable(
          name: "Wallet");

      migrationBuilder.DropTable(
          name: "Chapter");

      migrationBuilder.DropTable(
          name: "TranslationPermission");

      migrationBuilder.DropTable(
          name: "ModerationQueue");

      migrationBuilder.DropTable(
          name: "Series");

      migrationBuilder.DropTable(
          name: "TranslationTeam");

      migrationBuilder.DropTable(
          name: "CreatorProfile");

      migrationBuilder.DropTable(
          name: "Language");

      migrationBuilder.DropTable(
          name: "User");
    }
  }
}
