using Application.Interfaces.Notification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace mlndex_backend.BackgroundServices
{
    /// <summary>
    /// Background service that periodically deletes read notifications older than 7 days.
    /// Runs once every 6 hours.
    /// </summary>
    public class NotificationCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationCleanupService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

        public NotificationCleanupService(IServiceScopeFactory scopeFactory, ILogger<NotificationCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[NotificationCleanup] Service started. Will run every {Hours}h.", Interval.TotalHours);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    int cleaned = await notifService.CleanupOldNotificationsAsync(daysOld: 7);
                    if (cleaned > 0)
                        _logger.LogInformation("[NotificationCleanup] Deleted {Count} old read notifications.", cleaned);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[NotificationCleanup] Error during cleanup.");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
    }
}
