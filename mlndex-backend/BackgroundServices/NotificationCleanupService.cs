using Application.Interfaces.Notification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace mlndex_backend.BackgroundServices
{
  /// <summary>
  /// Background service that periodically deletes read notifications older than 7 days.
  /// Runs once every 6 hours.
  /// </summary>
  public class NotificationCleanupService : BaseBackgroundService
  {
    protected override TimeSpan Interval => TimeSpan.FromHours(6);
    protected override string ServiceName => "NotificationCleanup";

    public NotificationCleanupService(IServiceScopeFactory scopeFactory, ILogger<NotificationCleanupService> logger)
        : base(scopeFactory, logger)
    {
    }

    protected override async Task ExecuteJobAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
      var notifService = serviceProvider.GetRequiredService<INotificationService>();
      int cleaned = await notifService.CleanupOldNotificationsAsync(daysOld: 7);
      if (cleaned > 0)
        _logger.LogInformation("[{ServiceName}] Deleted {Count} old read notifications.", ServiceName, cleaned);
    }
  }
}
