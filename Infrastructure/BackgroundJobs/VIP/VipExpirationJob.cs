using Application.Interfaces.Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.BackgroundJobs.VIP
{
	public class VipExpirationJob : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger<VipExpirationJob> _logger;

		public VipExpirationJob(
			IServiceProvider serviceProvider,
			ILogger<VipExpirationJob> logger)
		{
			_serviceProvider = serviceProvider;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				using var scope = _serviceProvider.CreateScope();
				var context = scope.ServiceProvider.GetRequiredService<IMlndexDbContext>();

				var expiredSubs = await context.VipSubscriptions
					.Where(s => s.Status == SubscriptionStatus.ACTIVE
							 && s.EndDate <= DateTime.UtcNow)
					.ToListAsync(stoppingToken);

				if (expiredSubs.Any())
				{
					foreach (var sub in expiredSubs)
						sub.Status = SubscriptionStatus.EXPIRED;

					await context.SaveChangesAsync();
					_logger.LogInformation(
						"VipExpirationJob: expired {Count} subscriptions.", expiredSubs.Count);
				}

				await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
			}
		}
	}
}
