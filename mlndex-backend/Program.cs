using Application.Interfaces.AIModeration;
using Application.Interfaces.Chapter;
using Application.Interfaces.Translation;
using Application.Services.AIModeration;
using Application.Services.Chapter;
using Application.Services.Translation;
using Application.Interfaces.AIModeration;
using Infrastructure.Persistence.Data;
using Infrastructure.Adapters.AIModeration;
using Infrastructure.Adapters.Cloudinary;
using Infrastructure.Adapters.Moderation;
using Application.Interfaces.Data;
using Application.Interfaces.Moderation;
using Microsoft.EntityFrameworkCore;
using mlndex_backend.Extension;

namespace mlndex_backend
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			var PORT = Environment.GetEnvironmentVariable("PORT") ?? "5285";

			if (builder.Environment.IsProduction())
				builder.WebHost.UseUrls($"http://0.0.0.0:{PORT}");
			else
				builder.WebHost.UseUrls($"http://localhost:{PORT}");

			// Standard API Services
			builder.Services.AddControllers();
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddSwaggerGen();
			builder.Services.AddHttpContextAccessor();
			builder.Services.AddHttpClient();
			builder.Services.AddSignalR();

			// Database Configuration
			builder.Services.AddDbContext<MlndexDbContext>(options =>
				options.UseSqlServer(builder.Configuration.GetConnectionString("DB"),
				sqlOptions => sqlOptions.MigrationsAssembly("Infrastructure")
			));
			builder.Services.AddScoped<IMlndexDbContext>(provider => provider.GetRequiredService<MlndexDbContext>());

			// Storage & Content Services
			builder.Services.AddSingleton<IStorageService, CloudinaryService>();

			// Core Moderation Engine
			var moderationConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ModerationConfig");
			builder.Services.AddSingleton<IBlacklistProvider>(new BlacklistProvider(moderationConfigPath));
			builder.Services.AddScoped<IModerationService, Application.Services.AIModeration.ModerationService>();
			// AI & Chapter Processing
			builder.Services.AddScoped<IAiModerationClient, AiModerationClient>();
			builder.Services.AddScoped<Application.Interfaces.AIModeration.IModerationService, Application.Services.AIModeration.ModerationService>();
			builder.Services.AddScoped<IChapterPageService, ChapterPageService>();
			// Translation Team Services
			builder.Services.AddScoped<ITranslationTeamService, TranslationTeamService>();
			// Browsing & Reading Services
			builder.Services.AddScoped<Application.Interfaces.Series.ISeriesService, Infrastructure.Services.Series.SeriesService>();
			builder.Services.AddScoped<Application.Interfaces.Notification.INotificationService, Infrastructure.Services.Notification.NotificationService>();
			builder.Services.AddScoped<ITranslationService, TranslationService>();
			builder.Services.AddScoped<ITranslationPermissionService, TranslationPermissionService>();

			builder.Services.AddCors(options =>
			{
				options.AddPolicy("AllowSpecificOrigin", policy =>
				{
					policy
					.WithOrigins("http://localhost:3000", "https://learnlinkk.vercel.app")
					.AllowAnyHeader()
					.AllowAnyMethod()
					.AllowCredentials();
				});
			});

			var app = builder.Build();

			if (app.Environment.IsDevelopment())
			{
				app.UseSwagger();
				app.UseSwaggerUI();
				app.UseHttpsRedirection();
			}

			app.UseGlobalExceptionHandling();
			app.UseCors("AllowSpecificOrigin");
			app.UseAuthentication();
			app.UseAuthorization();
			app.UseStaticFiles();

			app.MapControllers();
			app.Run();
		}
	}
}
