using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.ReportSystem;
using Application.Services.ReportSystem;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.ReportSystem;

public class ReportSystem_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public ReportSystem_CsvAlignedTests(ITestOutputHelper output)
  {
    _output = output;
  }

  private static MlndexDbContext CreateInMemoryDbContext()
  {
    var options = new DbContextOptionsBuilder<MlndexDbContext>()
        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
        .Options;
    return new MlndexDbContext(options);
  }

  [Fact]
  public async Task CreateReportAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.Users.Add(new User { UserId = 1, Username = "reporter1", Email = "reporter1@test.com", DisplayName = "R1", PasswordHash = "hash" });
    db.Series.Add(new Series { SeriesId = 10, Title = "Test Series" });
    await db.SaveChangesAsync();

    var service = new PlagiarismReportService(db);
    var input = new CreatePlagiarismReportRequest
    {
      TargetType = ReportTargetType.Series,
      TargetId = 10,
      Reason = ReportReason.Plagiarism,
      Description = "Copied content",
      EvidenceUrls = new List<string> { "https://example.com/ev1" }
    };

    var output = await service.CreateReportAsync(1, input);

    _output.WriteLine($"Input: reporterId=1, targetId={input.TargetId}");
    _output.WriteLine($"Output: reportId={output.ReportId}, status={output.Status}");

    output.Status.Should().Be(ReportStatus.Pending);
    output.TargetId.Should().Be(10);
  }

  [Fact]
  public async Task CreateReportAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = new PlagiarismReportService(db);

    var input = new CreatePlagiarismReportRequest
    {
      TargetType = ReportTargetType.Series,
      TargetId = 10,
      Reason = ReportReason.Other,
      Description = "Invalid reporter"
    };

    var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateReportAsync(999, input));
    _output.WriteLine("Input: reporterId=999");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("User không tồn tại.");
  }

  [Fact]
  public async Task CreateReportAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    db.Users.Add(new User { UserId = 1, Username = "reporter1", Email = "reporter1@test.com", DisplayName = "R1", PasswordHash = "hash" });
    await db.SaveChangesAsync();

    var service = new PlagiarismReportService(db);

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.CreateReportAsync(1, null!));
    _output.WriteLine("Input: request=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task ResolveReportAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.Users.Add(new User { UserId = 1, Username = "reporter1", Email = "r1@test.com", DisplayName = "R1", PasswordHash = "hash" });
    db.Users.Add(new User { UserId = 2, Username = "baduser", Email = "b@test.com", DisplayName = "B1", PasswordHash = "hash", TrustScore = 100 });
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
    var output = await service.ResolveReportAsync(101, 3, new ResolvePlagiarismReportRequest
    {
      NewStatus = ReportStatus.Resolved,
      PenaltyScore = 20,
      ResolutionNotes = "Confirmed"
    });

    _output.WriteLine("Input: reportId=101, penalty=20");
    _output.WriteLine($"Output: status={output.Status}");

    output.Status.Should().Be(ReportStatus.Resolved);
    (await db.Users.FindAsync(2))!.TrustScore.Should().Be(80);
  }

  [Fact]
  public async Task ResolveReportAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = new PlagiarismReportService(db);

    var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ResolveReportAsync(-1, 1, new ResolvePlagiarismReportRequest
    {
      NewStatus = ReportStatus.Resolved
    }));

    _output.WriteLine("Input: reportId=-1");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("Report không tồn tại.");
  }

  [Fact]
  public async Task ResolveReportAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    db.Users.Add(new User { UserId = 1, Username = "reporter1", Email = "r1@test.com", DisplayName = "R1", PasswordHash = "hash" });
    db.Reports.Add(new Report
    {
      ReportId = 201,
      ReporterId = 1,
      ContentType = ReportTargetType.User,
      ContentId = 1,
      Status = ReportStatus.Pending,
      Reason = ReportReason.Other
    });
    await db.SaveChangesAsync();

    var service = new PlagiarismReportService(db);

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.ResolveReportAsync(201, 3, null!));
    _output.WriteLine("Input: request=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task RestoreTrustScoreAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.Users.Add(new User { UserId = 1, Username = "blocked", Email = "b@test.com", DisplayName = "B", PasswordHash = "h", TrustScore = 0, CannotUpload = true });
    await db.SaveChangesAsync();

    var service = new TrustScoreService(db);
    var output = await service.RestoreTrustScoreAsync(new RestoreTrustScoreRequest
    {
      TargetType = TrustScoreTargetType.User,
      TargetId = 1,
      ScoreToRestore = 30,
      Reason = "Good behavior"
    }, moderatorId: 99);

    _output.WriteLine("Input: restore 30 score for userId=1");
    _output.WriteLine($"Output: old={output.OldScore}, new={output.NewScore}");

    output.NewScore.Should().Be(30);
    output.CanUpload.Should().BeTrue();
  }

  [Fact]
  public async Task RestoreTrustScoreAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = new TrustScoreService(db);

    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreTrustScoreAsync(new RestoreTrustScoreRequest
    {
      TargetType = TrustScoreTargetType.User,
      TargetId = 1,
      ScoreToRestore = 0,
      Reason = "Invalid"
    }, moderatorId: 99));

    _output.WriteLine("Input: ScoreToRestore=0");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("Điểm phục hồi phải lớn hơn 0.");
  }

  [Fact]
  public async Task RestoreTrustScoreAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    var service = new TrustScoreService(db);

    var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RestoreTrustScoreAsync(new RestoreTrustScoreRequest
    {
      TargetType = TrustScoreTargetType.User,
      TargetId = 999,
      ScoreToRestore = 10,
      Reason = "Missing user"
    }, moderatorId: 99));

    _output.WriteLine("Input: missing user targetId=999");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("User không tồn tại.");
  }

  [Fact]
  public async Task CreateAppealAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.Users.Add(new User { UserId = 1, Username = "appealer", Email = "a@test.com", DisplayName = "A", PasswordHash = "h" });
    await db.SaveChangesAsync();

    var service = new TrustScoreService(db);
    var output = await service.CreateAppealAsync(1, new CreateAppealRequest
    {
      Reason = "I was wrongly penalized",
      EvidenceUrl = "https://example.com/proof"
    });

    _output.WriteLine("Input: userId=1 create appeal");
    _output.WriteLine($"Output: appealId={output.AppealId}, status={output.Status}");

    output.Status.Should().Be("Pending");
  }

  [Fact]
  public async Task CreateAppealAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = new TrustScoreService(db);

    var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateAppealAsync(999, new CreateAppealRequest
    {
      Reason = "Invalid user"
    }));

    _output.WriteLine("Input: userId=999");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("User không tồn tại.");
  }

  [Fact]
  public async Task CreateAppealAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    db.Users.Add(new User { UserId = 1, Username = "appealer", Email = "a@test.com", DisplayName = "A", PasswordHash = "h" });
    await db.SaveChangesAsync();

    var service = new TrustScoreService(db);

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.CreateAppealAsync(1, null!));
    _output.WriteLine("Input: request=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task ReviewAppealAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.Users.Add(new User { UserId = 1, Username = "appealer", Email = "a@test.com", DisplayName = "A", PasswordHash = "h", TrustScore = 10 });
    db.Appeals.Add(new Appeal { AppealId = 1, UserId = 1, Reason = "Wrong penalty", Status = AppealStatus.Pending });
    await db.SaveChangesAsync();

    var service = new TrustScoreService(db);
    var output = await service.ReviewAppealAsync(1, 99, new ReviewAppealRequest
    {
      IsApproved = true,
      ScoreToRestore = 40,
      ReviewNotes = "Verified"
    });

    _output.WriteLine("Input: approve appealId=1, score restore 40");
    _output.WriteLine($"Output: status={output.Status}, restored={output.ScoreRestored}");

    output.Status.Should().Be("Approved");
    output.ScoreRestored.Should().Be(40);
  }

  [Fact]
  public async Task ReviewAppealAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = new TrustScoreService(db);

    var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ReviewAppealAsync(-1, 99, new ReviewAppealRequest
    {
      IsApproved = false,
      ReviewNotes = "Invalid"
    }));

    _output.WriteLine("Input: appealId=-1");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("Appeal không tồn tại.");
  }

  [Fact]
  public async Task ReviewAppealAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    db.Users.Add(new User { UserId = 1, Username = "appealer", Email = "a@test.com", DisplayName = "A", PasswordHash = "h" });
    db.Appeals.Add(new Appeal { AppealId = 1, UserId = 1, Reason = "Wrong", Status = AppealStatus.Pending });
    await db.SaveChangesAsync();

    var service = new TrustScoreService(db);

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.ReviewAppealAsync(1, 99, null!));
    _output.WriteLine("Input: request=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }
}
