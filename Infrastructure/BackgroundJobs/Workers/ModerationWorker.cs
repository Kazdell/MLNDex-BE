// Infrastructure/BackgroundJobs/Workers/ModerationWorker.cs
// ═══════════════════════════════════════════════════════════
// ModerationWorker v3 — Best of Both Worlds
//
// From Main: Priority Queue, DB locking, Notification push,
//            3 SignalR events (Processing/Completed/Failed)
// From Backup: 429 retry, transient retry, Series moderation,
//              expanded crash recovery, MarkFailed
// ═══════════════════════════════════════════════════════════

using Application.DTOs.AIModeration;
using Application.Interfaces.AIModeration;
using Application.Interfaces.Data;
using Application.Interfaces.Notification;
using Domain.Entities;
using Infrastructure.Hubs;
using ModerationSignalQueue = Infrastructure.BackgroundJobs.Queue.ModerationQueue;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Infrastructure.BackgroundJobs.Workers;

public class ModerationWorker : BackgroundService
{
    private readonly ModerationSignalQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ModerationWorker> _logger;

    // Concurrency: max 3 AI calls in parallel
    private const int MAX_CONCURRENT = 3;
    private const int MAX_RETRIES = 3;
    private readonly SemaphoreSlim _semaphore = new(MAX_CONCURRENT);

    public ModerationWorker(
        ModerationSignalQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ModerationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ── Startup: Crash Recovery ────────────────────────────────────────────
    // Combines Main (IN_REVIEW reset) + Backup (orphan PENDING scan)
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[ModerationWorker] Khởi động — kiểm tra job bị dở...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IMlndexDbContext>();
            var resumeCount = 0;

            // ① Resume jobs stuck in IN_REVIEW (server crashed mid-processing)
            var stuckJobs = await db.ModerationQueues
                .Where(q => q.Status == QueueStatus.IN_REVIEW)
                .ToListAsync(cancellationToken);

            foreach (var job in stuckJobs)
            {
                job.Status = QueueStatus.PENDING;
                job.AssignedAt = null;
                await _queue.EnqueueAsync(job.ContentId, cancellationToken);
                resumeCount++;
            }

            // ② Re-enqueue orphan chapters (PENDING in Chapters but no queue entry)
            var orphanChapterIds = await db.Chapters
                .Where(c => c.ModerationStatus == ModerationStatus.PENDING)
                .Where(c => !db.ModerationQueues.Any(q =>
                    q.ContentId == c.ChapterId
                    && q.ContentType == ModerationQueueContentType.CHAPTER
                    && q.Status == QueueStatus.PENDING))
                .Select(c => c.ChapterId)
                .ToListAsync(cancellationToken);

            foreach (var chId in orphanChapterIds)
            {
                db.ModerationQueues.Add(new Domain.Entities.ModerationQueue
                {
                    ContentId = chId,
                    ContentType = ModerationQueueContentType.CHAPTER,
                    Status = QueueStatus.PENDING,
                    FlaggedAt = DateTime.UtcNow,
                    Priority = QueuePriority.MEDIUM
                });
                await _queue.EnqueueAsync(chId, cancellationToken);
                resumeCount++;
            }

            // ③ Re-enqueue orphan series
            var orphanSeriesIds = await db.Series
                .Where(s => s.ModerationStatus == ModerationStatus.PENDING)
                .Where(s => !db.ModerationQueues.Any(q =>
                    q.ContentId == s.SeriesId
                    && q.ContentType == ModerationQueueContentType.SERIES
                    && q.Status == QueueStatus.PENDING))
                .Select(s => s.SeriesId)
                .ToListAsync(cancellationToken);

            foreach (var sId in orphanSeriesIds)
            {
                db.ModerationQueues.Add(new Domain.Entities.ModerationQueue
                {
                    ContentId = sId,
                    ContentType = ModerationQueueContentType.SERIES,
                    Status = QueueStatus.PENDING,
                    FlaggedAt = DateTime.UtcNow,
                    Priority = QueuePriority.MEDIUM
                });
                await _queue.EnqueueAsync(sId, cancellationToken);
                resumeCount++;
            }

            if (resumeCount > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(
                    "[ModerationWorker] Crash recovery: resume {Count} jobs " +
                    "({Stuck} stuck, {OrphanCh} orphan chapters, {OrphanSr} orphan series)",
                    resumeCount, stuckJobs.Count,
                    orphanChapterIds.Count, orphanSeriesIds.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ModerationWorker] Crash recovery error — worker sẽ vẫn start");
        }

        await base.StartAsync(cancellationToken);
    }

    // ── Main loop: listen for signals from Channel ─────────────────────────
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[ModerationWorker] Bắt đầu lắng nghe queue.");

        await foreach (var signal in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            // Fire-and-forget with semaphore — don't block the loop
            _ = ProcessNextPendingAsync(stoppingToken);
        }
    }

    // ── Pick next PENDING job from DB (priority-ordered) ───────────────────
    private async Task ProcessNextPendingAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IMlndexDbContext>();

            // Priority: HIGH first, then FIFO by FlaggedAt
            var job = await db.ModerationQueues
                .Where(q => q.Status == QueueStatus.PENDING)
                .OrderBy(q => q.Priority == QueuePriority.HIGH ? 0
                            : q.Priority == QueuePriority.URGENT ? 0 : 1)
                    .ThenBy(q => q.FlaggedAt)
                .FirstOrDefaultAsync(ct);

            if (job == null) return;

            // Lock the job
            job.Status = QueueStatus.IN_REVIEW;
            job.AssignedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "[ModerationWorker] Processing {ContentType} ContentId={ContentId} (retry {Retry})",
                job.ContentType, job.ContentId, job.RetryCount);

            // Route to correct handler
            if (job.ContentType == ModerationQueueContentType.SERIES)
                await RunSeriesModerationAsync(job.ContentId, scope, ct);
            else
                await RunChapterModerationAsync(job.ContentId, scope, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ModerationWorker] Error picking next job");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // ── Chapter Moderation (Main flow + Backup retry) ──────────────────────
    private async Task RunChapterModerationAsync(
        int chapterId,
        IServiceScope scope,
        CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<IMlndexDbContext>();
        var moderationService = scope.ServiceProvider.GetRequiredService<IModerationService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var notificationPusher = scope.ServiceProvider.GetRequiredService<INotificationPusher>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ModerationHub>>();

        // Push "processing" status to frontend
        await hubContext.Clients
            .Group($"chapter_{chapterId}")
            .SendAsync("ModerationProcessing", chapterId, ct);

        try
        {
            // ── Run AI moderation ──────────────────────────────────────
            AiModerationResultDto result =
                await moderationService.RunAiModerationAsync(chapterId);

            // ── Update queue status in DB ──────────────────────────────
            var job = await db.ModerationQueues
                .FirstOrDefaultAsync(q => q.ContentId == chapterId
                                       && q.Status == QueueStatus.IN_REVIEW, ct);
            if (job != null)
            {
                job.Status = QueueStatus.RESOLVED;
                job.AssignedTo = null; // null = processed by AI
                await db.SaveChangesAsync(ct);
            }

            _logger.LogInformation(
                "[ModerationWorker] ChapterId={ChapterId} hoàn tất. Flagged={Flagged}.",
                chapterId, result.Flagged);

            // ── Push result via SignalR ─────────────────────────────────
            await hubContext.Clients
                .Group($"chapter_{chapterId}")
                .SendAsync("ModerationCompleted", new { chapterId, result }, ct);

            // ── Create notification for creator ────────────────────────
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

            // Save notification to DB
            var notification = await notificationService.CreateNotificationAsync(
                creatorId,
                "AI Kiểm duyệt",
                message,
                link,
                result.Flagged
                    ? NotificationType.CONTENT_REJECTED
                    : NotificationType.CONTENT_APPROVED
            );

            // Push notification bell via SignalR
            await notificationPusher.PushNotificationAsync(creatorId, notification);
        }
        // ── 429 Rate Limit — exponential backoff (FROM BACKUP) ─────────
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            await HandleRetryAsync(db, chapterId, ex,
                isRateLimit: true, hubContext: hubContext, ct: ct);
        }
        // ── Transient errors — shorter backoff (FROM BACKUP) ───────────
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ModerationWorker] Error processing ChapterId={ChapterId}", chapterId);

            await HandleRetryAsync(db, chapterId, ex,
                isRateLimit: false, hubContext: hubContext, ct: ct);
        }
    }

    // ── Series Moderation (FROM BACKUP) ────────────────────────────────────
    private async Task RunSeriesModerationAsync(
        int seriesId,
        IServiceScope scope,
        CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<IMlndexDbContext>();
        var moderationService = scope.ServiceProvider.GetRequiredService<IModerationService>();

        try
        {
            await moderationService.RunSeriesModerationAsync(seriesId);

            // Mark job as RESOLVED
            var job = await db.ModerationQueues
                .FirstOrDefaultAsync(q => q.ContentId == seriesId
                                       && q.ContentType == ModerationQueueContentType.SERIES
                                       && q.Status == QueueStatus.IN_REVIEW, ct);
            if (job != null)
            {
                job.Status = QueueStatus.RESOLVED;
                await db.SaveChangesAsync(ct);
            }

            _logger.LogInformation("[ModerationWorker] Series {SeriesId} moderation complete", seriesId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ModerationWorker] Series {SeriesId} moderation failed", seriesId);

            // Mark DISMISSED on failure
            var job = await db.ModerationQueues
                .FirstOrDefaultAsync(q => q.ContentId == seriesId
                                       && q.ContentType == ModerationQueueContentType.SERIES, ct);
            if (job != null)
            {
                job.Status = QueueStatus.DISMISSED;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    // ── Retry logic (FROM BACKUP — adapted for DB queue) ──────────────────
    private async Task HandleRetryAsync(
        IMlndexDbContext db,
        int contentId,
        Exception ex,
        bool isRateLimit,
        IHubContext<ModerationHub>? hubContext,
        CancellationToken ct)
    {
        var job = await db.ModerationQueues
            .FirstOrDefaultAsync(q => q.ContentId == contentId
                                   && q.Status == QueueStatus.IN_REVIEW, ct);

        var retryCount = job?.RetryCount ?? 0;

        if (retryCount < MAX_RETRIES)
        {
            // Rate limit: longer backoff (15s, 45s, 135s)
            // Transient: shorter backoff (2s, 4s, 8s)
            var delaySeconds = isRateLimit
                ? (int)Math.Pow(3, retryCount + 1) * 5
                : (int)Math.Pow(2, retryCount + 1);

            _logger.LogWarning(
                "[ModerationWorker] {Type} for ContentId={Id}. Retry {N}/{Max} in {Delay}s",
                isRateLimit ? "429 Rate limit" : "Transient error",
                contentId, retryCount + 1, MAX_RETRIES, delaySeconds);

            // Reset job to PENDING for retry
            if (job != null)
            {
                job.Status = QueueStatus.PENDING;
                job.AssignedAt = null;
                job.RetryCount += 1;
                await db.SaveChangesAsync(ct);
            }

            // Delayed re-enqueue
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
                    await _queue.EnqueueAsync(contentId, ct);
                }
                catch { /* ignore cancellation */ }
            }, ct);
        }
        else
        {
            _logger.LogError(ex,
                "[ModerationWorker] Max retries ({Max}) for ContentId={Id}. Marking DISMISSED.",
                MAX_RETRIES, contentId);

            await MarkFailedAsync(db, contentId,
                isRateLimit ? "AI_RATE_LIMIT_EXCEEDED" : ex.Message, ct);

            // Notify frontend of failure
            if (hubContext != null)
            {
                await hubContext.Clients
                    .Group($"chapter_{contentId}")
                    .SendAsync("ModerationFailed", new
                    {
                        chapterId = contentId,
                        message = "Kiểm duyệt thất bại sau nhiều lần thử. Vui lòng liên hệ hỗ trợ."
                    }, ct);
            }
        }
    }

    // ── Mark job as failed + save error to AiScoresJson (FROM BACKUP) ─────
    private static async Task MarkFailedAsync(
        IMlndexDbContext db,
        int contentId,
        string errorMsg,
        CancellationToken ct)
    {
        // Update queue status
        var job = await db.ModerationQueues
            .FirstOrDefaultAsync(q => q.ContentId == contentId, ct);
        if (job != null)
        {
            job.Status = QueueStatus.DISMISSED;
            await db.SaveChangesAsync(ct);
        }

        // Save error info to chapter AiScoresJson
        var chapter = await db.Chapters.FindAsync(new object[] { contentId }, ct);
        if (chapter != null)
        {
            chapter.AiScoresJson = JsonSerializer.Serialize(new
            {
                error = errorMsg,
                failedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
    }
}
