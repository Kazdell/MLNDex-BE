using Microsoft.EntityFrameworkCore;
using Mlndex.Data;
using mlndex_backend.Extension;
using Application.Interfaces.Moderation;
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

			// Moderation Service (Person #3)
			var moderationConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ModerationConfig");
			builder.Services.AddSingleton(new BlacklistProvider(moderationConfigPath));
			builder.Services.AddScoped<IModerationService, ModerationService>();

			// Translation Team Service (Person Dung)
			builder.Services.AddScoped<ITranslationTeamService, TranslationTeamService>();
			builder.Services.AddScoped<ITranslationService, TranslationService>();
			builder.Services.AddScoped<ITranslationPermissionService, TranslationPermissionService>();

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
