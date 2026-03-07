using Application.Interfaces.AIModeration;
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
using Application.Services.Community;
using Application.Services.Creator;
using Application.Services.Financial;
using Application.Services.Moderation;
using Application.Services.System;
using Application.Services.Translation;
using Application.Services.User;
using Infrastructure.Adapters.AIModeration;
using Infrastructure.Adapters.Cloudinary;
using Infrastructure.Adapters.Moderation;
using Infrastructure.Adapters.Tesseract;
using Infrastructure.Persistence.Data;
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
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DB"),
                    sqlOptions =>
                        sqlOptions.MigrationsAssembly("Infrastructure").EnableRetryOnFailure()
                )
            );
            builder.Services.AddScoped<IMlndexDbContext>(provider =>
                provider.GetRequiredService<MlndexDbContext>()
            );

            // Storage & Content Services
            builder.Services.AddSingleton<IStorageService, CloudinaryService>();

            // Core Moderation Engine
            var moderationConfigPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "ModerationConfig"
            );
            builder.Services.AddSingleton<IBlacklistProvider>(
                new BlacklistProvider(moderationConfigPath)
            );
            builder.Services.AddScoped<
                IModerationService,
                Application.Services.AIModeration.ModerationService
            >();

            // AI & Novel Processing
            builder.Services.AddScoped<IAiModerationClient, AiModerationClient>();
            builder.Services.AddScoped<IOCRService, TesseractOCRService>();
            builder.Services.AddScoped<
                Application.Interfaces.AIModeration.IModerationService,
                Application.Services.AIModeration.ModerationService
            >();
            builder.Services.AddScoped<ISeriesService, SeriesService>();
            builder.Services.AddScoped<IChapterPageService, ChapterPageService>();
            builder.Services.AddScoped<IChapterService, ChapterService>();
            builder.Services.AddScoped<IGenreService, GenreService>();
            builder.Services.AddScoped<IWithdrawalService, WithdrawalService>();
            builder.Services.AddScoped<IModeratorAdminService, ModeratorAdminService>();
            builder.Services.AddScoped<IFinancialReportService, FinancialReportService>();
            builder.Services.AddScoped<IReportService, ReportService>();
            builder.Services.AddScoped<ICommentService, CommentService>();
            builder.Services.AddScoped<IAccountModerationService, AccountModerationService>();
            builder.Services.AddScoped<IContentModerationService, ContentModerationService>();
            builder.Services.AddSingleton<ISystemConfigService>(sp =>
            {
                var path = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "SystemConfig",
                    "system-settings.json"
                );
                return new SystemConfigService(path);
            });

            // Translation Team Services
            builder.Services.AddScoped<ITranslationTeamService, TranslationTeamService>();
            builder.Services.AddScoped<ITranslationService, TranslationService>();
            builder.Services.AddScoped<
                ITranslationPermissionService,
                TranslationPermissionService
            >();
            builder.Services.AddScoped<IHistoryService, HistoryService>();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy(
                    "AllowSpecificOrigin",
                    policy =>
                    {
                        policy
                            .WithOrigins("http://localhost:5173")
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();
                    }
                );
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
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MlndexDbContext>();
                db.Database.Migrate();
            }
            app.Run();
        }
    }
}
