using Application.Interfaces.AIModeration;
using Application.Interfaces.Chapter;
using Infrastructure.Services.AIModeration;
using Infrastructure.Services.Chapter;
using Microsoft.EntityFrameworkCore;
using Mlndex.Data;
using mlndex_backend.Extension;
using Application.Interfaces.Translation;
using Infrastructure.Services.Moderation;
using Infrastructure.Services.Translation;

namespace mlndex_backend
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			//var PORT = Environment.GetEnvironmentVariable("PORT") ?? "8888";

			//if(builder.Environment.IsProduction())
			//{
			//	builder.WebHost.UseUrls($"http://0.0.0.0:{PORT}");
			//}
			//else
			//{
			//	builder.WebHost.UseUrls($"http://localhost:{PORT}");
			//}
			var PORT = Environment.GetEnvironmentVariable("PORT") ?? "5285";

			if (builder.Environment.IsProduction())
				builder.WebHost.UseUrls($"http://0.0.0.0:{PORT}");
			else
				builder.WebHost.UseUrls($"http://localhost:{PORT}");

			builder.Services.AddControllers();
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddSwaggerGen();

			builder.Services.AddDbContext<MlndexDbContext>(options =>
				options.UseSqlServer(builder.Configuration.GetConnectionString("DB"),
				sqlOptions => sqlOptions.MigrationsAssembly("Infrastructure")
			));

			

			builder.Services.AddHttpClient();

			// === Service ===
			builder.Services.AddSingleton<IStorageService, CloudinaryService>();
			builder.Services.AddScoped<IAiModerationClient, AiModerationClient>();
			builder.Services.AddScoped<IModerationService, ModerationService>();
			builder.Services.AddScoped<IChapterPageService, ChapterPageService>();
			// Translation Team Service
			builder.Services.AddScoped<ITranslationTeamService, TranslationTeamService>();
			builder.Services.AddScoped<ITranslationService, TranslationService>();
			builder.Services.AddScoped<ITranslationPermissionService, TranslationPermissionService>();
			// ============================


			builder.Services.AddHttpContextAccessor();


			// Add SignalR
			// Moderation Service (Person #3)
			var moderationConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ModerationConfig");
			builder.Services.AddSingleton(new BlacklistProvider(moderationConfigPath));

			builder.Services.AddSignalR();

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
