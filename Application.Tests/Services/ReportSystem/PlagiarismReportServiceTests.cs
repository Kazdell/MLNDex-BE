using System;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.ReportSystem;
using Application.Services.ReportSystem;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.Tests.Services.ReportSystem
{
    public class PlagiarismReportServiceTests
    {
        private MlndexDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<MlndexDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new MlndexDbContext(options);
        }

        [Fact]
        public async Task CreateReportAsync_ShouldCreatePendingReport_WhenUserExists()
        {
            var db = CreateInMemoryDbContext();
            db.Users.Add(new User { UserId = 1, Username = "reporter1", Email = "reporter1@gmail.com", DisplayName = "R1", PasswordHash = "hash" });
            db.Series.Add(new Series { SeriesId = 10, Title = "Test Series" });
            await db.SaveChangesAsync();

            var service = new PlagiarismReportService(db);
            var request = new CreatePlagiarismReportRequest
            {
                TargetType = ReportTargetType.Series,
                TargetId = 10,
                Reason = ReportReason.Plagiarism,
                Description = "This is copied"
            };

            var result = await service.CreateReportAsync(1, request);

            result.Should().NotBeNull();
            result.Status.Should().Be(ReportStatus.Pending);
            result.TargetType.Should().Be(ReportTargetType.Series);

            var reportInDb = await db.Reports.FirstOrDefaultAsync(r => r.ReportId == result.ReportId);
            reportInDb.Should().NotBeNull();
            reportInDb?.ContentType.Should().Be(ReportTargetType.Series);
        }

        [Fact]
        public async Task ResolveReportAsync_ShouldApplyPenaltyToTeam_WhenTargetIsTeam()
        {
            var db = CreateInMemoryDbContext();
            db.Users.Add(new User { UserId = 1, Username = "reporter1", Email = "reporter1@gmail.com", DisplayName = "R1", PasswordHash = "hash" });
            db.TranslationTeams.Add(new TranslationTeam { TeamId = 5, TeamName = "Team A", Slug = "team-a", TrustScore = 100 });
            db.Reports.Add(new Report
            {
                ReportId = 100,
                ReporterId = 1,
                ContentType = ReportTargetType.Team,
                ContentId = 5,
                Status = ReportStatus.Pending,
                Reason = ReportReason.Plagiarism
            });
            await db.SaveChangesAsync();

            var service = new PlagiarismReportService(db);
            var request = new ResolvePlagiarismReportRequest
            {
                NewStatus = ReportStatus.Resolved,
                PenaltyScore = 50,
                ResolutionNotes = "Plagiarism confirmed"
            };

            var result = await service.ResolveReportAsync(100, 2, request);

            result.Status.Should().Be(ReportStatus.Resolved);

            var teamInDb = await db.TranslationTeams.FindAsync(5);
            teamInDb?.TrustScore.Should().Be(50); // 100 - 50

            var history = await db.TrustScoreHistories.FirstOrDefaultAsync(h => h.TranslationTeamId == 5);
            history.Should().NotBeNull();
            history?.ScoreChange.Should().Be(-50);
            history?.RelatedReportId.Should().Be(100);
        }

        [Fact]
        public async Task ResolveReportAsync_ShouldApplyPenaltyToUser_WhenTargetIsUser()
        {
            var db = CreateInMemoryDbContext();
            db.Users.Add(new User { UserId = 1, Username = "reporter1", Email = "reporter1@test.com", DisplayName = "R1", PasswordHash = "hash" });
            db.Users.Add(new User { UserId = 2, Username = "baduser", Email = "baduser@test.com", TrustScore = 100, DisplayName = "B1", PasswordHash = "hash" });
            db.Reports.Add(new Report
            {
                ReportId = 101,
                ReporterId = 1,
                ContentType = ReportTargetType.User,
                ContentId = 2,
                Status = ReportStatus.Pending,
                Reason = ReportReason.Other
            });
            await db.SaveChangesAsync();

            var service = new PlagiarismReportService(db);
            var request = new ResolvePlagiarismReportRequest
            {
                NewStatus = ReportStatus.Resolved,
                PenaltyScore = 20,
                ResolutionNotes = "Spamming confirmed"
            };

            var result = await service.ResolveReportAsync(101, 3, request);

            result.Status.Should().Be(ReportStatus.Resolved);

            var userInDb = await db.Users.FindAsync(2);
            userInDb?.TrustScore.Should().Be(80); // 100 - 20

            var history = await db.TrustScoreHistories.FirstOrDefaultAsync(h => h.UserId == 2);
            history.Should().NotBeNull();
            history?.ScoreChange.Should().Be(-20);
            history?.RelatedReportId.Should().Be(101);
        }

        [Fact]
        public async Task ResolveReportAsync_ShouldStrikeContent_WhenStrikeIsTrue()
        {
            var db = CreateInMemoryDbContext();
            db.Users.Add(new User { UserId = 1, Username = "reporter1", Email = "reporter1@gmail.com", DisplayName = "R1", PasswordHash = "hash" });
            db.Translations.Add(new Domain.Entities.Translation { TranslationId = 10, ModerationStatus = ModerationStatus.APPROVED, LanguageId = 1, ChapterId = 1, PermissionId = 1 });
            db.Reports.Add(new Report
            {
                ReportId = 102,
                ReporterId = 1,
                ContentType = ReportTargetType.ChapterTranslation,
                ContentId = 10,
                Status = ReportStatus.Investigating,
                Reason = ReportReason.Plagiarism
            });
            await db.SaveChangesAsync();

            var service = new PlagiarismReportService(db);
            var request = new ResolvePlagiarismReportRequest
            {
                NewStatus = ReportStatus.Resolved,
                StrikeContent = true,
                ResolutionNotes = "Struck content"
            };

            var result = await service.ResolveReportAsync(102, 2, request);

            var transInDb = await db.Translations.FindAsync(10);
            transInDb?.ModerationStatus.Should().Be(ModerationStatus.REJECTED);
        }
    }
}
