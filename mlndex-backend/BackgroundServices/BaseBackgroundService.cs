using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace mlndex_backend.BackgroundServices
{
  /// <summary>
  /// Base class for periodic background services.
  /// Handles loop execution, logging, exception catching, and creating dependency injection scopes.
  /// </summary>
  public abstract class BaseBackgroundService : BackgroundService
  {
    protected readonly IServiceScopeFactory _scopeFactory;
    protected readonly ILogger _logger;

    /// <summary>
    /// How often the service should run.
    /// </summary>
    protected abstract TimeSpan Interval { get; }

    /// <summary>
    /// Name of the service for logging purposes.
    /// </summary>
    protected abstract string ServiceName { get; }

    protected BaseBackgroundService(IServiceScopeFactory scopeFactory, ILogger logger)
    {
      _scopeFactory = scopeFactory;
      _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      _logger.LogInformation("[{ServiceName}] Service started. Will run every {Hours}h.", ServiceName, Interval.TotalHours);

      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          using var scope = _scopeFactory.CreateScope();
          await ExecuteJobAsync(scope.ServiceProvider, stoppingToken);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "[{ServiceName}] Error during execution.", ServiceName);
        }

        await Task.Delay(Interval, stoppingToken);
      }
    }

    /// <summary>
    /// The actual job logic to execute per interval.
    /// A scoped ServiceProvider is provided for retrieving scoped services.
    /// </summary>
    protected abstract Task ExecuteJobAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken);
  }
}
