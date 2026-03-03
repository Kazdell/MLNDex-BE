using Application.Interfaces.AIModeration;
using Application.Interfaces.Chapter;
using Infrastructure.Services.AIModeration;
using Infrastructure.Services.Chapter;
using Microsoft.EntityFrameworkCore;
using Mlndex.Data;
using mlndex_backend.Extension;
using Application.Interfaces.Translation;
using Infrastructure.Services.Translation;

using CoreModeration = Application.Interfaces.Moderation;
using CoreModerationImpl = Infrastructure.Services.Moderation;

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

			// Storage & Content Services
			builder.Services.AddSingleton<IStorageService, CloudinaryService>();

			// Moderation Service (Person #3 Content Policy Engine)
			var moderationConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ModerationConfig");
			builder.Services.AddSingleton(new CoreModerationImpl.BlacklistProvider(moderationConfigPath));
			builder.Services.AddScoped<CoreModeration.IModerationService, CoreModerationImpl.ModerationService>();
			
			// AI Moderation (Automation Layer)
			builder.Services.AddScoped<IAiModerationClient, AiModerationClient>();
			builder.Services.AddScoped<Application.Interfaces.AIModeration.IModerationService, Infrastructure.Services.AIModeration.ModerationService>();
			builder.Services.AddScoped<IChapterPageService, ChapterPageService>();

			// Translation Team Services
			builder.Services.AddScoped<ITranslationTeamService, TranslationTeamService>();
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
