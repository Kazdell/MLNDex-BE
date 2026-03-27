using Application.Interfaces.Data;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.Services.BackgroundJobs
{
    /// <summary>
    /// Background job chạy mỗi 15 phút, quét tất cả chapter có LockStatus=LOCKED
    /// và UnlockTime <= UtcNow, rồi flip sang UNLOCKED và clear coin price.
    /// </summary>
    public class ChapterUnlockWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ChapterUnlockWorker> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(15);

        public ChapterUnlockWorker(IServiceScopeFactory scopeFactory, ILogger<ChapterUnlockWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[ChapterUnlockJob] Started. Interval: {Interval} phút.", _interval.TotalMinutes);

            // Delay nhỏ khi app mới khởi động để tránh tranh DB migration
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await UnlockExpiredChaptersAsync(stoppingToken);
                    await Task.Delay(_interval, stoppingToken); // ← trong try
                }
                catch (OperationCanceledException)
                {
                    break; // shutdown bình thường
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ChapterUnlockJob] Lỗi khi sweep unlock. Sẽ thử lại sau {Interval} phút.", _interval.TotalMinutes);
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }

            }

            _logger.LogInformation("[ChapterUnlockJob] Stopped.");
        }

        private async Task UnlockExpiredChaptersAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IMlndexDbContext>();

            var now = DateTime.UtcNow;

            // Chỉ load những field cần thiết để tránh over-fetch
            var expiredChapters = await db.Chapters
                .Where(c => c.LockStatus == ChapterLockStatus.LOCKED
                         && c.UnlockTime.HasValue
                         && c.UnlockTime.Value <= now)
                .ToListAsync(ct);

            if (!expiredChapters.Any())
            {
                _logger.LogDebug("[ChapterUnlockJob] Không có chapter nào cần unlock lúc {Time}.", now);
                return;
            }

            foreach (var chapter in expiredChapters)
            {
                chapter.LockStatus = ChapterLockStatus.UNLOCKED;
                chapter.UnlockTime = null;
                chapter.UnlockPriceCoins = null;
                chapter.UpdatedAt = now; // ← thêm nếu entity có field này
            }

            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "[ChapterUnlockJob] Đã unlock {Count} chapter. IDs: [{Ids}]",
                expiredChapters.Count,
                string.Join(", ", expiredChapters.Select(c => c.ChapterId)));
        }
    }
}
