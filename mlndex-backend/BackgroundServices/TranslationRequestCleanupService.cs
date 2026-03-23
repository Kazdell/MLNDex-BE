using Application.Interfaces.Translation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace mlndex_backend.BackgroundServices
{
  /// <summary>
  /// Background service that periodically rejects translation requests
  /// that have been pending for more than 72 hours.
  /// Runs once every hour.
  /// </summary>
  public class TranslationRequestCleanupService : BaseBackgroundService
  {
    protected override TimeSpan Interval => TimeSpan.FromHours(1);
    protected override string ServiceName => "TranslationRequestCleanup";

    public TranslationRequestCleanupService(IServiceScopeFactory scopeFactory, ILogger<TranslationRequestCleanupService> logger)
        : base(scopeFactory, logger)
    {
    }

    protected override async Task ExecuteJobAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
      var translationPermissionService = serviceProvider.GetRequiredService<ITranslationPermissionService>();

      // Auto-deny requests older than 72 hours
      int deniedCount = await translationPermissionService.AutoDenyExpiredRequestsAsync(expireHours: 72);

      if (deniedCount > 0)
      {
        _logger.LogInformation("[{ServiceName}] Auto-denied {Count} translation requests older than 72h.", ServiceName, deniedCount);
      }
    }
  }
}
