using Application.Interfaces;
using Application.Interfaces.AIModeration;
using Application.Interfaces.Auth;
using Application.Interfaces.Common;
using Application.Interfaces.Community;
using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Application.Interfaces.Financial;
using Application.Interfaces.Moderation;
using Application.Interfaces.Notification;
using Application.Interfaces.Payment;
using Application.Interfaces.Services;
using Application.Interfaces.System;
using Application.Interfaces.Translation;
using Application.Interfaces.User;
using Application.Services;
using Application.Services.AIModeration;
using Application.Services.Auth;
using Application.Services.BackgroundJobs;
using Application.Services.Community;
using Application.Services.Creator;
using Application.Services.Financial;
using Application.Services.Moderation;
using Application.Services.Payment;
using Application.Services.System;
using Application.Services.Translation;
using Application.Services.User;
using Infrastructure.Adapters.AIModeration;
using Infrastructure.Adapters.Cloudinary;
using Infrastructure.Adapters.Moderation;
using Infrastructure.Adapters.OCR;
using Infrastructure.Adapters.Translation;
using Infrastructure.Common;
using Infrastructure.DI;
using Infrastructure.Hubs;
using Infrastructure.Persistence.Data;
using Infrastructure.Services;
using Infrastructure.Services.Auth;
using Infrastructure.Services.Notification;
using Infrastructure.Services.Payment;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using mlndex_backend.Extension;
using mlndex_backend.Hubs;
using System.Text;
using System.Text.Json;
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
					options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
					options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
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
			builder.Services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
			builder.Services.AddScoped<IFacebookOAuthService, FacebookOAuthService>();


			// Storage & Content Services
			builder.Services.AddSingleton<IStorageService, CloudinaryService>();

			// Creator Services
			builder.Services.AddScoped<ISeriesService, SeriesService>();
			builder.Services.AddScoped<IRecommendationService, RecommendationService>();
			builder.Services.AddScoped<IChapterService, ChapterService>();
			builder.Services.AddScoped<IGenreService, GenreService>();
			builder.Services.AddScoped<ICreatorService, CreatorService>();

			// Core Moderation Engine
			var moderationConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ModerationConfig");
			builder.Services.AddSingleton<IBlacklistProvider>(new BlacklistProvider(moderationConfigPath));
			builder.Services.AddScoped<IModerationService, ModerationService>();
			builder.Services.AddScoped<IReportService, ReportService>();
			builder.Services.AddScoped<IAccountModerationService, AccountModerationService>();
			builder.Services.AddScoped<IModeratorAdminService, ModeratorAdminService>();
			builder.Services.AddScoped<ICommentModerationService, CommentModerationService>();

			// Isolated Report & TrustScore Services
			builder.Services.AddScoped<Application.Interfaces.ReportSystem.IPlagiarismReportService, Application.Services.ReportSystem.PlagiarismReportService>();
			builder.Services.AddScoped<Application.Interfaces.ReportSystem.ITrustScoreService, Application.Services.ReportSystem.TrustScoreService>();

			// AI & Chapter Processing
			builder.Services.AddSingleton<Infrastructure.BackgroundJobs.Queue.ModerationQueue>();
			builder.Services.AddSingleton<Application.Interfaces.Queue.IModerationQueue>(sp =>
				sp.GetRequiredService<Infrastructure.BackgroundJobs.Queue.ModerationQueue>());
			builder.Services.AddHostedService<Infrastructure.BackgroundJobs.Workers.ModerationWorker>();
			builder.Services.AddHostedService<mlndex_backend.BackgroundServices.NotificationCleanupService>();
			builder.Services.AddHostedService<mlndex_backend.BackgroundServices.TranslationRequestCleanupService>();
            builder.Services.AddHostedService<ChapterUnlockWorker>();

            builder.Services.AddHttpClient();
            builder.Services.AddScoped<IAiModerationClient, AiModerationClient>();

			// Translation Team Services
			builder.Services.AddScoped<ITranslationTeamService, TranslationTeamService>();
			builder.Services.AddScoped<ITranslationService, TranslationService>();
			builder.Services.AddScoped<IPageTranslationService, PageTranslationService>();
			builder.Services.AddScoped<ITranslationPermissionService, TranslationPermissionService>();

			// OCR Services — Tesseract fallback (Scoped)
			// For single IOCRService injection (PageTranslationService, ModerationService):
			//   → last registration wins = Tesseract
			builder.Services.AddScoped<TesseractOCRService>();
			builder.Services.AddScoped<IOCRService>(sp => sp.GetRequiredService<TesseractOCRService>());
			builder.Services.AddScoped<IAiTranslationClient, AiTranslationClient>();
			builder.Services.AddScoped<IGoogleTranslationClient, GoogleTranslationClient>();
			builder.Services.AddScoped<IReaderTranslationService, ReaderTranslationService>();

			// Community Services
			builder.Services.AddScoped<ICommentService, CommentService>();
			builder.Services.AddScoped<ILikeService, LikeService>();

			// User Services
			builder.Services.AddScoped<IHistoryService, HistoryService>();
			builder.Services.AddScoped<IUserService, UserService>();
			builder.Services.AddScoped<IFollowService, FollowService>();
			builder.Services.AddScoped<IRatingService, RatingService>();
			builder.Services.AddScoped<IBookmarkService, BookmarkService>();
			builder.Services.AddScoped<IUserListService, UserListService>();

			// Notification Services
			builder.Services.AddScoped<INotificationPusher, NotificationPusher>();
			builder.Services.AddScoped<INotificationService, NotificationService>();

			// Financial Services
			builder.Services.AddScoped<IFinancialReportService, FinancialReportService>();
			builder.Services.AddScoped<IWithdrawalService, WithdrawalService>();
			builder.Services.AddScoped<IPaymentGatewayFactory, PaymentGatewayFactory>();
			builder.Services.AddScoped<ITopUpService, TopUpService>();
			builder.Services.AddScoped<ICoinRateService, CoinRateService>();
			builder.Services.AddScoped<IPaymentGatewayService, PayOsGatewayService>();
			builder.Services.AddScoped<ICoinPackageService, CoinPackageService>();
	

			// System Services — SystemConfigService now uses DbContext
			builder.Services.AddScoped<ISystemConfigService, SystemConfigService>();

			// Increase timeout
			builder.Services.Configure<KestrelServerOptions>(options =>
			{
				options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(20);
				options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(20);
			});


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
				// ← Thêm đoạn này để SignalR đọc được token từ query string
				item.Events = new JwtBearerEvents
				{
					OnMessageReceived = context =>
					{
						var accessToken = context.Request.Query["access_token"];
						var path = context.HttpContext.Request.Path;
						if (!string.IsNullOrEmpty(accessToken) &&
								  path.StartsWithSegments("/hubs"))
						{
							context.Token = accessToken;
						}
						return Task.CompletedTask;
					}
				};
			});

			// Build allowed origins list: always include localhost + any production origins from env
			var allowedOrigins = new List<string>
			{
				"http://localhost:5173", "http://127.0.0.1:5173",
				"http://localhost:5174", "http://127.0.0.1:5174",
				"http://localhost:5175", "http://127.0.0.1:5175"
			};
			var extraOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
			if (!string.IsNullOrEmpty(extraOrigins))
			{
				allowedOrigins.AddRange(extraOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
			}

			builder.Services.AddCors(options =>
			{
				options.AddPolicy("AllowSpecificOrigin", policy =>
				{
					policy
								.WithOrigins(allowedOrigins.ToArray())
								.AllowAnyHeader()
								.AllowAnyMethod()
								.AllowCredentials();
				});
			});

			var app = builder.Build();

			// Swagger enabled for all environments (Production + Development)
			app.UseSwagger();
			app.UseSwaggerUI();

			if (app.Environment.IsDevelopment())
			{
			}

			app.UseGlobalExceptionHandling();
			app.UseCors("AllowSpecificOrigin");
			app.UseAuthentication();
			app.UseAuthorization();
			app.UseStaticFiles();

			app.MapHub<NotificationHub>("/hubs/notification");
			app.MapHub<ModerationHub>("/hubs/moderation");

			app.MapControllers();
			/*
			using (var scope = app.Services.CreateScope())
			{
				var db = scope.ServiceProvider.GetRequiredService<MlndexDbContext>();
				db.Database.Migrate();
			}
			*/
			app.Run();
		}
	}
}