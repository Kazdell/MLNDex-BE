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

            // ② Re-enqueue orphan chapters (PENDING in Chapters but NO queue entry at all)
            //    Chapters already processed (RESOLVED/DISMISSED) should NOT be re-queued
            var orphanChapterIds = await db.Chapters
     .Where(c => c.ModerationStatus == ModerationStatus.PENDING
              && c.AiScoresJson == null)  // 👈 không re-enqueue human review
     .Where(c => !db.ModerationQueues.Any(q =>
         q.ContentId == c.ChapterId
         && q.ContentType == ModerationQueueContentType.CHAPTER
         && (q.Status == QueueStatus.PENDING
             || q.Status == QueueStatus.IN_REVIEW
             || q.Status == QueueStatus.RESOLVED)))
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
                    && (q.Status == QueueStatus.PENDING
                        || q.Status == QueueStatus.IN_REVIEW
                        || q.Status == QueueStatus.RESOLVED)))
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

            // ④ Re-enqueue orphan translations
            var orphanTranslationIds = await db.Translations
                .Where(t => t.ModerationStatus == ModerationStatus.PENDING)
                .Where(t => !db.ModerationQueues.Any(q =>
                    q.ContentId == t.TranslationId
                    && q.ContentType == ModerationQueueContentType.TRANSLATION
                    && (q.Status == QueueStatus.PENDING
                        || q.Status == QueueStatus.IN_REVIEW
                        || q.Status == QueueStatus.RESOLVED)))
                .Select(t => t.TranslationId)
                .ToListAsync(cancellationToken);

            foreach (var tId in orphanTranslationIds)
            {
                db.ModerationQueues.Add(new Domain.Entities.ModerationQueue
                {
                    ContentId = tId,
                    ContentType = ModerationQueueContentType.TRANSLATION,
                    Status = QueueStatus.PENDING,
                    FlaggedAt = DateTime.UtcNow,
                    Priority = QueuePriority.MEDIUM
                });
                await _queue.EnqueueAsync(tId, cancellationToken);
                resumeCount++;
            }

            if (resumeCount > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(
                    "[ModerationWorker] Crash recovery: resume {Count} jobs " +
                    "({Stuck} stuck, {OrphanCh} orphan chapters, {OrphanSr} orphan series, {OrphanTr} orphan translations)",
                    resumeCount, stuckJobs.Count,
                    orphanChapterIds.Count, orphanSeriesIds.Count, orphanTranslationIds.Count);
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

        // Process any jobs already PENDING in the database on startup
        _ = ProcessAllPendingAsync(stoppingToken);

        await foreach (var signal in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            // Process all pending jobs whenever a new signal arrives
            _ = ProcessAllPendingAsync(stoppingToken);
        }
    }

    // ── Safe concurrent dispatcher ─────────────────────────────────────────
    // ProcessAllPendingAsync — fix với optimistic concurrency
    private async Task ProcessAllPendingAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            if (stoppingToken.IsCancellationRequested) break;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IMlndexDbContext>();

            var job = await db.ModerationQueues
                .Where(q => q.Status == QueueStatus.PENDING)
                .OrderBy(q => q.Priority == QueuePriority.HIGH || q.Priority == QueuePriority.URGENT ? 0 : 1)
                .ThenBy(q => q.FlaggedAt)
                .FirstOrDefaultAsync(stoppingToken);

            if (job == null) break;

            // ── Thêm guard: skip human review queue ───────────────────
            if (job.ContentType == ModerationQueueContentType.CHAPTER)
            {
                var chapter = await db.Chapters
                    .FirstOrDefaultAsync(c => c.ChapterId == job.ContentId, stoppingToken);

                if (chapter == null)
                {
                    // Chapter bị xóa → dismiss job
                    job.Status = QueueStatus.DISMISSED;
                    await db.SaveChangesAsync(stoppingToken);
                    continue; // ← tiếp tục job khác, không break
                }

                // Chỉ skip nếu chapter KHÔNG phải PENDING
                // (đã được human review xong, không cần AI chạy lại)
                if (chapter.AiScoresJson != null
                    && chapter.ModerationStatus != ModerationStatus.PENDING)
                {
                    job.Status = QueueStatus.RESOLVED;  // ← RESOLVED thay vì PENDING
                    await db.SaveChangesAsync(stoppingToken);
                    continue; // ← continue thay vì break
                }

                // Nếu chapter.ModerationStatus == PENDING (vừa edit xong)
                // thì clear AiScoresJson để chạy lại AI
                if (chapter.ModerationStatus == ModerationStatus.PENDING)
                {
                    chapter.AiScoresJson = null;
                    await db.SaveChangesAsync(stoppingToken);
                }
            }

            job.Status = QueueStatus.IN_REVIEW;
            job.AssignedAt = DateTime.UtcNow;

            try
            {
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogDebug(
                    "[ModerationWorker] Concurrency conflict trên QueueId={Id}, thử job khác.",
                    job.QueueId);
                continue;
            }

            await _semaphore.WaitAsync(stoppingToken);

            _ = Task.Run(async () =>
            {
                try
                {
                    using var innerScope = _scopeFactory.CreateScope();

                    _logger.LogInformation(
                        "[ModerationWorker] Processing {ContentType} ContentId={ContentId} (retry {Retry})",
                        job.ContentType, job.ContentId, job.RetryCount);

                    if (job.ContentType == ModerationQueueContentType.SERIES)
                        await RunSeriesModerationAsync(job.ContentId, innerScope, stoppingToken);
                    else if (job.ContentType == ModerationQueueContentType.TRANSLATION)
                        await RunTranslationModerationAsync(job.ContentId, innerScope, stoppingToken);
                    else
                        await RunChapterModerationAsync(job.ContentId, innerScope, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ModerationWorker] Error during background AI task");
                }
                finally
                {
                    _semaphore.Release();
                }
            }, stoppingToken);
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
            var moderationService2 = scope.ServiceProvider.GetRequiredService<IModerationService>();
            var verifiedResult = await moderationService2.GetResultAsync(chapterId, ct)
                ?? result; // fallback về result gốc nếu query lỗi

            await hubContext.Clients
                .Group($"chapter_{chapterId}")
                .SendAsync("ModerationCompleted", new { chapterId, result = verifiedResult }, ct);

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
            await HandleRetryAsync(db, chapterId, ModerationQueueContentType.CHAPTER, ex,
                isRateLimit: true, hubContext: hubContext, ct: ct);
        }
        // ── Transient errors — shorter backoff (FROM BACKUP) ───────────
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ModerationWorker] Error processing ChapterId={ChapterId}", chapterId);

            await HandleRetryAsync(db, chapterId, ModerationQueueContentType.CHAPTER, ex,
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

    // ── Translation Moderation ──────────────────────────────────────────
    private async Task RunTranslationModerationAsync(
        int translationId,
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
            .Group($"translation_{translationId}")
            .SendAsync("ModerationProcessing", translationId, ct);

        try
        {
            // ── Run AI moderation ──────────────────────────────────────
            AiModerationResultDto result =
                await moderationService.RunTranslationModerationAsync(translationId);

            // ── Update queue status in DB ──────────────────────────────
            var job = await db.ModerationQueues
                .FirstOrDefaultAsync(q => q.ContentId == translationId
                                       && q.ContentType == ModerationQueueContentType.TRANSLATION
                                       && q.Status == QueueStatus.IN_REVIEW, ct);
            if (job != null)
            {
                job.Status = QueueStatus.RESOLVED;
                job.AssignedTo = null; // null = processed by AI
                await db.SaveChangesAsync(ct);
            }

            _logger.LogInformation(
                "[ModerationWorker] TranslationId={TranslationId} hoàn tất. Flagged={Flagged}.",
                translationId, result.Flagged);

            // ── Push result via SignalR ─────────────────────────────────
            await hubContext.Clients
                .Group($"translation_{translationId}")
                .SendAsync("ModerationCompleted", new { translationId, result }, ct);

            // ── Create notification for creator ────────────────────────
            var translation = await db.Translations
                .Include(t => t.Chapter)
                    .ThenInclude(c => c.Series)
                .Include(t => t.Team)
                    .ThenInclude(team => team!.TeamMembers)
                .FirstOrDefaultAsync(t => t.TranslationId == translationId, ct);

            if (translation?.Chapter?.Series != null && translation.Team != null)
            {
                var owners = translation!.Team!.TeamMembers
                    .Where(m => m.Role == Domain.Entities.TeamMemberRole.LEADER && m.IsActive)
                    .ToList();

                var message = result.Flagged
                    ? $"Bản dịch chương {translation.Chapter.ChapterNumber} của \"{translation.Chapter.Series.Title}\" bị gắn cờ vi phạm"
                    : $"Bản dịch chương {translation.Chapter.ChapterNumber} của \"{translation.Chapter.Series.Title}\" đã qua kiểm duyệt";
                var link = $"/creator/moderation-result?translationId={translationId}";

                foreach (var owner in owners)
                {
                    // Save notification to DB
                    var notification = await notificationService.CreateNotificationAsync(
                        owner.UserId,
                        "AI Kiểm duyệt",
                        message,
                        link,
                        result.Flagged
                            ? NotificationType.CONTENT_REJECTED
                            : NotificationType.CONTENT_APPROVED
                    );

                    // Push notification bell via SignalR
                    await notificationPusher.PushNotificationAsync(owner.UserId, notification);
                }
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            await HandleRetryAsync(db, translationId, ModerationQueueContentType.TRANSLATION, ex,
                isRateLimit: true, hubContext: hubContext, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ModerationWorker] Error processing TranslationId={TranslationId}", translationId);

            await HandleRetryAsync(db, translationId, ModerationQueueContentType.TRANSLATION, ex,
                isRateLimit: false, hubContext: hubContext, ct: ct);
        }
    }

    // ── Retry logic (FROM BACKUP — adapted for DB queue) ──────────────────
    private async Task HandleRetryAsync(
        IMlndexDbContext db,
        int contentId,
        ModerationQueueContentType contentType,
        Exception ex,
        bool isRateLimit,
        IHubContext<ModerationHub>? hubContext,
        CancellationToken ct)
    {
        var job = await db.ModerationQueues
            .FirstOrDefaultAsync(q => q.ContentId == contentId
                                   && q.ContentType == contentType
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
                "[ModerationWorker] Max retries ({Max}) for ContentId={Id} Type={Type}. Marking DISMISSED.",
                MAX_RETRIES, contentId, contentType);

            await MarkFailedAsync(db, contentId, contentType,
                isRateLimit ? "AI_RATE_LIMIT_EXCEEDED" : ex.Message, ct);

            // Notify frontend of failure
            if (hubContext != null)
            {
                var groupName = contentType == ModerationQueueContentType.CHAPTER ? $"chapter_{contentId}" : $"translation_{contentId}";
                await hubContext.Clients
                    .Group(groupName)
                    .SendAsync("ModerationFailed", new
                    {
                        id = contentId,
                        message = "Kiểm duyệt thất bại sau nhiều lần thử. Vui lòng liên hệ hỗ trợ."
                    }, ct);
            }
        }
    }

    // ── Mark job as failed + save error to AiScoresJson (FROM BACKUP) ─────
    private static async Task MarkFailedAsync(
        IMlndexDbContext db,
        int contentId,
        ModerationQueueContentType contentType,
        string errorMsg,
        CancellationToken ct)
    {
        // Update queue status
        var job = await db.ModerationQueues
            .FirstOrDefaultAsync(q => q.ContentId == contentId && q.ContentType == contentType, ct);
        if (job != null)
        {
            job.Status = QueueStatus.DISMISSED;
            await db.SaveChangesAsync(ct);
        }

        // Save error info to AiScoresJson
        if (contentType == ModerationQueueContentType.CHAPTER)
        {
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
        else if (contentType == ModerationQueueContentType.TRANSLATION)
        {
            var translation = await db.Translations.FindAsync(new object[] { contentId }, ct);
            if (translation != null)
            {
                translation.AiScoresJson = JsonSerializer.Serialize(new
                {
                    error = errorMsg,
                    failedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
