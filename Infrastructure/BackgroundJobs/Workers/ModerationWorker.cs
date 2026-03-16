// Infrastructure/BackgroundJobs/Workers/ModerationWorker.cs
using Application.DTOs.AIModeration;
using Application.DTOs.Notification;
using Application.Interfaces.AIModeration;
using Application.Interfaces.Data;
using Application.Interfaces.Notification;
using Domain.Entities;
using Infrastructure.BackgroundJobs.Queue;
using Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModerationQueue = Infrastructure.BackgroundJobs.Queue.ModerationQueue;

namespace Infrastructure.BackgroundJobs.Workers;

public class ModerationWorker : BackgroundService
{
    private readonly ModerationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ModerationWorker> _logger;

    // Tối đa 3 chapter được AI xử lý song song
    private readonly SemaphoreSlim _semaphore = new(3);

    public ModerationWorker(
        ModerationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ModerationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ── Startup: resume các job bị kẹt do server crash ────────────────────
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[ModerationWorker] Khởi động, kiểm tra job bị dở...");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IMlndexDbContext>();

        var stuckJobs = await db.ModerationQueues
            .Where(q => q.Status == QueueStatus.IN_REVIEW)
            .ToListAsync(cancellationToken);

        foreach (var job in stuckJobs)
        {
            job.Status = QueueStatus.PENDING;
            job.AssignedAt = null;
            await _queue.EnqueueAsync(job.ContentId, cancellationToken);
        }

        if (stuckJobs.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                "[ModerationWorker] Resume {Count} job bị dở.", stuckJobs.Count);
        }

        await base.StartAsync(cancellationToken);
    }

    // ── Main loop: lắng nghe signal từ Channel ────────────────────────────
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[ModerationWorker] Bắt đầu lắng nghe queue.");

        await foreach (var signal in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            // Không await — lấy job tiếp ngay, semaphore giới hạn concurrency
            _ = ProcessNextPendingAsync(stoppingToken);
        }
    }

    // ── Lấy job PENDING từ DB và xử lý ───────────────────────────────────
    private async Task ProcessNextPendingAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IMlndexDbContext>();

            // HIGH trước, sau đó FIFO theo FlaggedAt
            var job = await db.ModerationQueues
                .Where(q => q.Status == QueueStatus.PENDING)
                .OrderBy(q => q.Priority == QueuePriority.HIGH ? 0 : 1)
                    .ThenBy(q => q.FlaggedAt)
                .FirstOrDefaultAsync(ct);

            if (job == null) return;

            // Lock job
            job.Status = QueueStatus.IN_REVIEW;
            job.AssignedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "[ModerationWorker] Bắt đầu xử lý ChapterId={ChapterId}.", job.ContentId);

            await RunModerationAsync(job.ContentId, scope, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ModerationWorker] Lỗi không mong đợi khi lấy job.");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // ── Chạy AI và xử lý kết quả ─────────────────────────────────────────
    private async Task RunModerationAsync(
        int chapterId,
        IServiceScope scope,
        CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<IMlndexDbContext>();
        var moderationService = scope.ServiceProvider.GetRequiredService<IModerationService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var notificationPusher = scope.ServiceProvider.GetRequiredService<INotificationPusher>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ModerationHub>>();

        // Thông báo frontend: AI đang xử lý
        await hubContext.Clients
            .Group($"chapter_{chapterId}")
            .SendAsync("ModerationProcessing", chapterId, ct);

        try
        {
            // ── Chạy AI ───────────────────────────────────────────────────
            AiModerationResultDto result =
                await moderationService.RunAiModerationAsync(chapterId);

            // ── Cập nhật ModerationQueue trong DB ─────────────────────────
            var job = await db.ModerationQueues
                .FirstOrDefaultAsync(q => q.ContentId == chapterId, ct);

            if (job != null)
            {
                job.Status = QueueStatus.RESOLVED;
                job.AssignedTo = null; // null = xử lý bởi hệ thống AI
                await db.SaveChangesAsync(ct);
            }

            _logger.LogInformation(
                "[ModerationWorker] ChapterId={ChapterId} hoàn tất. Flagged={Flagged}.",
                chapterId, result.Flagged);

            // ── Push kết quả về trang /moderation-result ──────────────────
            await hubContext.Clients
                .Group($"chapter_{chapterId}")
                .SendAsync("ModerationCompleted", new { chapterId, result }, ct);

            // ── Lấy thông tin chapter ──────────────────────────────────────
            var chapter = await db.Chapters
                .Include(c => c.Series)
                    .ThenInclude(s => s.Creator)
                .FirstOrDefaultAsync(c => c.ChapterId == chapterId, ct);

            if (chapter?.Series?.Creator == null) return;

            var creatorId = chapter.Series.Creator.UserId;
            var message = result.Flagged
                ? $"Chương {chapter.ChapterNumber} của \"{chapter.Series.Title}\" bị gắn cờ vi phạm"
                : $"Chương {chapter.ChapterNumber} của \"{chapter.Series.Title}\" đã qua kiểm duyệt";
            var link = $"/creator/moderation-result?chapterId={chapterId}";

            // ── Lưu notification vào DB ────────────────────────────────────
            var notification = await notificationService.CreateNotificationAsync(
                creatorId,
                "AI Kiểm duyệt",
                message,
                link,
                result.Flagged
                    ? NotificationType.CONTENT_REJECTED
                    : NotificationType.CONTENT_APPROVED
            );

            // ── Push notification bell realtime qua NotificationHub ────────
            // Dùng INotificationPusher — group "User_{id}" nhất quán với NotificationHub
            await notificationPusher.PushNotificationAsync(creatorId, notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ModerationWorker] AI thất bại cho ChapterId={ChapterId}.", chapterId);

            // ── Mark DISMISSED trong DB ────────────────────────────────────
            var job = await db.ModerationQueues
                .FirstOrDefaultAsync(q => q.ContentId == chapterId, ct);

            if (job != null)
            {
                job.Status = QueueStatus.DISMISSED;
                await db.SaveChangesAsync(ct);
            }

            // ── Thông báo frontend thất bại ────────────────────────────────
            await hubContext.Clients
                .Group($"chapter_{chapterId}")
                .SendAsync("ModerationFailed", new
                {
                    chapterId,
                    message = "Kiểm duyệt thất bại, vui lòng liên hệ hỗ trợ."
                }, ct);
        }
    }
}
