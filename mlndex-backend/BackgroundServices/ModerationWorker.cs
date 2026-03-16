using Application.DTOs.AIModeration;
using Application.Interfaces.AIModeration;
using Application.Interfaces.Data;
using Domain.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using mlndex_backend.Hubs;
using System.Text.Json;

namespace mlndex_backend.BackgroundServices
{
    /// <summary>
    /// Background worker that consumes ModerationJobs from the queue,
    /// calls ModerationService, and pushes results via SignalR.
    /// 
    /// Features:
    /// - Crash Recovery: On startup, re-enqueues chapters stuck in PENDING
    /// - Exponential Backoff: Retries on 429 (AI rate limit) up to 3 times
    /// - Concurrency Limit: SemaphoreSlim(3) to avoid overwhelming AI API
    /// </summary>
    public class ModerationWorker : BackgroundService
    {
        private readonly IModerationQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<ModerationHub> _hubContext;
        private readonly ILogger<ModerationWorker> _logger;

        private const int MAX_RETRIES = 3;
        private const int MAX_CONCURRENT = 3;
        private readonly SemaphoreSlim _semaphore = new(MAX_CONCURRENT);

        public ModerationWorker(
            IModerationQueue queue,
            IServiceScopeFactory scopeFactory,
            IHubContext<ModerationHub> hubContext,
            ILogger<ModerationWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// On startup, find all chapters stuck in PENDING status and re-enqueue them.
        /// This handles crash recovery when the server restarts mid-moderation.
        /// </summary>
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ModerationWorker starting — checking for stuck PENDING chapters and series...");

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IMlndexDbContext>();

                var pendingChapterIds = await db.Chapters
                    .Where(c => c.ModerationStatus == ModerationStatus.PENDING)
                    .Select(c => c.ChapterId)
                    .ToListAsync(cancellationToken);

                foreach (var chId in pendingChapterIds)
                {
                    await _queue.EnqueueAsync(
                        new ModerationJob(chId, ModerationContentType.Chapter),
                        cancellationToken);
                    _logger.LogInformation("Re-enqueued stuck chapter {ChapterId}", chId);
                }

                if (pendingChapterIds.Count > 0)
                    _logger.LogWarning("Crash recovery: re-enqueued {Count} stuck chapters", pendingChapterIds.Count);

                var pendingSeriesIds = await db.Series
                    .Where(s => s.ModerationStatus == ModerationStatus.PENDING)
                    .Select(s => s.SeriesId)
                    .ToListAsync(cancellationToken);

                foreach (var sId in pendingSeriesIds)
                {
                    await _queue.EnqueueAsync(
                        new ModerationJob(sId, ModerationContentType.Series),
                        cancellationToken);
                    _logger.LogInformation("Re-enqueued stuck series {SeriesId}", sId);
                }

                if (pendingSeriesIds.Count > 0)
                    _logger.LogWarning("Crash recovery: re-enqueued {Count} stuck series", pendingSeriesIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during crash recovery — worker will still start");
            }

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ModerationWorker running — waiting for jobs...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var job = await _queue.DequeueAsync(stoppingToken);
                    // Fire and track with semaphore, don't await to allow concurrency
                    _ = ProcessJobAsync(job, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error dequeuing moderation job");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        private async Task ProcessJobAsync(ModerationJob job, CancellationToken ct)
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                _logger.LogInformation("Processing moderation for {ContentType} {ContentId} (retry {Retry})",
                    job.ContentType, job.ContentId, job.RetryCount);

                using var scope = _scopeFactory.CreateScope();
                var moderationService = scope.ServiceProvider.GetRequiredService<IModerationService>();
                var db = scope.ServiceProvider.GetRequiredService<IMlndexDbContext>();

                if (job.ContentType == ModerationContentType.Chapter)
                {
                    // Run AI moderation
                    var result = await moderationService.RunAiModerationAsync(job.ContentId);

                    // Save AI scores JSON
                    var chapter = await db.Chapters
                        .Include(c => c.Series)
                        .FirstOrDefaultAsync(c => c.ChapterId == job.ContentId, ct);
                    if (chapter != null)
                    {
                        chapter.AiScoresJson = JsonSerializer.Serialize(result.CategoryScores);
                        await db.SaveChangesAsync(ct);

                        // Push result via SignalR
                        var statusDto = new ModerationStatusDto
                        {
                            ChapterId = job.ContentId,
                            Status = chapter.ModerationStatus.ToString(),
                            Flagged = result.Flagged,
                            FlaggedReason = result.FlaggedReason,
                            CategoryScores = result.CategoryScores,
                            UpdatedAt = DateTime.UtcNow
                        };

                        await _hubContext.Clients
                            .Group($"Chapter_{job.ContentId}")
                            .SendAsync("ReceiveModerationResult", statusDto, ct);

                        await _hubContext.Clients
                            .Group($"User_{chapter.Series.CreatorId}")
                            .SendAsync("ReceiveModerationResult", statusDto, ct);
                    }
                }
                else if (job.ContentType == ModerationContentType.Series)
                {
                    await moderationService.RunSeriesModerationAsync(job.ContentId);
                    
                    var series = await db.Series.FirstOrDefaultAsync(s => s.SeriesId == job.ContentId, ct);
                    if (series != null)
                    {
                        var statusDto = new ModerationStatusDto
                        {
                            ChapterId = job.ContentId,
                            Status = series.ModerationStatus.ToString(),
                            UpdatedAt = DateTime.UtcNow
                        };

                        await _hubContext.Clients
                            .Group($"Series_{job.ContentId}")
                            .SendAsync("ReceiveSeriesModerationResult", statusDto, ct);

                        await _hubContext.Clients
                            .Group($"User_{series.CreatorId}")
                            .SendAsync("ReceiveSeriesModerationResult", statusDto, ct);
                    }
                }

                _logger.LogInformation("{ContentType} {ContentId} moderation complete", job.ContentType, job.ContentId);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                // AI API rate limit (429) — exponential backoff
                if (job.RetryCount < MAX_RETRIES)
                {
                    var delaySeconds = (int)Math.Pow(3, job.RetryCount + 1) * 5; // 5s, 15s, 45s
                    _logger.LogWarning(
                        "AI rate limit hit for {ContentType} {ContentId}. Retry {Retry}/{Max} in {Delay}s",
                        job.ContentType, job.ContentId, job.RetryCount + 1, MAX_RETRIES, delaySeconds);

                    _ = Task.Run(async () => {
                        try {
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
                            await _queue.EnqueueAsync(job with { RetryCount = job.RetryCount + 1 }, ct);
                        } catch { /* ignore cancellation */ }
                    }, ct);
                }
                else
                {
                    _logger.LogError(
                        "AI rate limit: max retries exceeded for {ContentType} {ContentId}. Marking as FAILED.",
                        job.ContentType, job.ContentId);
                    await MarkFailedAsync(job, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing moderation for {ContentType} {ContentId}", job.ContentType, job.ContentId);

                // Retry on transient errors
                if (job.RetryCount < MAX_RETRIES)
                {
                    var delaySeconds = (int)Math.Pow(2, job.RetryCount + 1); // 2s, 4s, 8s
                    _logger.LogWarning("Retrying {ContentType} {ContentId} in {Delay}s", job.ContentType, job.ContentId, delaySeconds);
                    _ = Task.Run(async () => {
                        try {
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
                            await _queue.EnqueueAsync(job with { RetryCount = job.RetryCount + 1 }, ct);
                        } catch { /* ignore cancellation */ }
                    }, ct);
                }
                else
                {
                    _logger.LogError("Max retries for {ContentType} {ContentId} — marking PENDING for manual review",
                        job.ContentType, job.ContentId);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task MarkFailedAsync(ModerationJob job, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IMlndexDbContext>();

                if (job.ContentType == ModerationContentType.Chapter)
                {
                    var chapter = await db.Chapters.FindAsync(new object[] { job.ContentId }, ct);
                    if (chapter != null)
                    {
                        chapter.AiScoresJson = JsonSerializer.Serialize(new { error = "AI_RATE_LIMIT_EXCEEDED" });
                        await db.SaveChangesAsync(ct);
                    }
                }
                else if (job.ContentType == ModerationContentType.Series)
                {
                    // Series doesn't have AiScoresJson yet, so we just log it
                    // The Series remains PENDING which is correct.
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark {ContentType} {ContentId} as failed", job.ContentType, job.ContentId);
            }
        }
    }
}
