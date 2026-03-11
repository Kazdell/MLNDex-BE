using Application.Interfaces.AIModeration;
using Application.Interfaces.Auth;
using Application.Interfaces.Common;
using Application.Interfaces.Community;
using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Application.Interfaces.Financial;
using Application.Interfaces.Moderation;
using Application.Interfaces.Notification;
using Application.Interfaces.System;
using Application.Interfaces.Translation;
using Application.Interfaces.User;
using Application.Services.AIModeration;
using Application.Services.Auth;
using Application.Services.Community;
using Application.Services.Creator;
using Application.Services.Financial;
using Application.Services.Moderation;
using Application.Services.System;
using Application.Services.Translation;
using Application.Services.User;
using Infrastructure.Common;
using Infrastructure.Adapters.AIModeration;
using Infrastructure.Adapters.Cloudinary;
using Infrastructure.Adapters.Moderation;
using Infrastructure.Adapters.Tesseract;
using Infrastructure.Persistence.Data;
using Infrastructure.Services.Auth;
using Infrastructure.Services.Notification;
using mlndex_backend.Hubs;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using mlndex_backend.Extension;
using System.Text;
using System.Text.Json.Serialization;

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
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddHttpClient();
            builder.Services.AddSignalR();

	  // ── Memory Cache (cho OTP + Token Blacklist) ────────────
	  builder.Services.AddMemoryCache();

			// Database Configuration
	  builder.Services.AddDbContext<MlndexDbContext>(options =>
          options.UseSqlServer(builder.Configuration.GetConnectionString("DB"),
          sqlOptions => sqlOptions.MigrationsAssembly("Infrastructure")
              .EnableRetryOnFailure()
      ));
      builder.Services.AddScoped<IMlndexDbContext>(provider => provider.GetRequiredService<MlndexDbContext>());

			// ── Auth Services ───────────────────────────────────────
			builder.Services.AddScoped<IAuthService, AuthService>();
			builder.Services.AddScoped<ITokenService, TokenService>();
			builder.Services.AddScoped<IOtpService, OtpService>();
			builder.Services.AddScoped<IEmailService, EmailService>();
			builder.Services.AddScoped<IUserContext, UserContext>();

			// Storage & Content Services
			builder.Services.AddSingleton<IStorageService, CloudinaryService>();

			// Creator Services
			builder.Services.AddScoped<ISeriesService, SeriesService>();
			builder.Services.AddScoped<IChapterService, ChapterService>();
			builder.Services.AddScoped<IGenreService, GenreService>();

			// Core Moderation Engine
			var moderationConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ModerationConfig");
			builder.Services.AddSingleton<IBlacklistProvider>(new BlacklistProvider(moderationConfigPath));
			builder.Services.AddScoped<IModerationService, ModerationService>();
			builder.Services.AddScoped<IReportService, ReportService>();
			builder.Services.AddScoped<IAccountModerationService, AccountModerationService>();
			builder.Services.AddScoped<IModeratorAdminService, ModeratorAdminService>();
			
			// AI & Chapter Processing
			builder.Services.AddScoped<IAiModerationClient, AiModerationClient>();
			
			builder.Services.AddScoped<IChapterPageService, ChapterPageService>();

			// Translation Team Services
			builder.Services.AddScoped<ITranslationTeamService, TranslationTeamService>();
			builder.Services.AddScoped<ITranslationService, TranslationService>();
			builder.Services.AddScoped<ITranslationPermissionService, TranslationPermissionService>();
            
            // OCR Services
            builder.Services.AddScoped<IOCRService, TesseractOCRService>();

			// Community Services
			builder.Services.AddScoped<ICommentService, CommentService>();
			builder.Services.AddScoped<ILikeService, LikeService>();

			// User Services
			builder.Services.AddScoped<IHistoryService, HistoryService>();
			builder.Services.AddScoped<IUserService, UserService>();

			// Notification Services
			builder.Services.AddScoped<INotificationPusher, NotificationPusher>();
			builder.Services.AddScoped<INotificationService, NotificationService>();

			// Financial Services
			builder.Services.AddScoped<IFinancialReportService, FinancialReportService>();
			builder.Services.AddScoped<IWithdrawalService, WithdrawalService>();

			// System Services — SystemConfigService cần filePath nên dùng factory
			var systemConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SystemConfig", "config.json");
			builder.Services.AddScoped<ISystemConfigService>(_ => new SystemConfigService(systemConfigFilePath));


			//enable jwt token
			var _authkey = builder.Configuration.GetValue<string>("JwtSettings:securitykey");
			builder.Services.AddAuthentication(item =>
			{
				item.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
				item.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
			}).AddJwtBearer(item =>
			{
				item.RequireHttpsMetadata = false;
				item.SaveToken = true;
				item.TokenValidationParameters = new TokenValidationParameters()
				{
					ValidateIssuerSigningKey = true,
					IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_authkey)),
					ValidateIssuer = false,
					ValidateAudience = false,
					ValidateLifetime = true,
					ClockSkew = TimeSpan.FromMinutes(5)
				};

			});

			builder.Services.AddCors(options =>
      {
        options.AddPolicy("AllowSpecificOrigin", policy =>
              {
            policy
                  .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
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
                // app.UseHttpsRedirection(); // Tắt để đồng bộ với local HTTP dev
            }

            app.UseGlobalExceptionHandling();
            app.UseCors("AllowSpecificOrigin");
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseStaticFiles();

            app.MapHub<NotificationHub>("/hubs/notification");

            app.MapControllers();
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MlndexDbContext>();
                db.Database.Migrate();
            }
            app.Run();
        }
    }
}
