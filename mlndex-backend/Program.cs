using Application.Interfaces.AIModeration;
using Application.Interfaces.Auth;
using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Application.Interfaces.Financial;
using Application.Interfaces.Moderation;
using Application.Interfaces.Notification;
using Application.Interfaces.System;
using Application.Interfaces.Translation;
using Application.Services.AIModeration;
using Application.Services.Auth;
using Application.Services.Creator;
using Application.Services.Financial;
using Application.Services.Moderation;
using Application.Services.System;
using Application.Services.Translation;
using Infrastructure.Adapters.AIModeration;
using Infrastructure.Adapters.Cloudinary;
using Infrastructure.Adapters.Moderation;
using Infrastructure.Adapters.Tesseract;
using Infrastructure.Persistence.Data;
using Infrastructure.Services.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using mlndex_backend.Extension;
using System.Text;

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

	  // ── Memory Cache (cho OTP + Token Blacklist) ────────────
	  builder.Services.AddMemoryCache();

            // Database Configuration
            builder.Services.AddDbContext<MlndexDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DB"),
                sqlOptions => sqlOptions.MigrationsAssembly("Infrastructure")
                    .EnableRetryOnFailure()
                    
            )
				.EnableSensitiveDataLogging());
      builder.Services.AddScoped<IMlndexDbContext>(provider => provider.GetRequiredService<MlndexDbContext>());

			// ── Auth Services ───────────────────────────────────────
			builder.Services.AddScoped<IAuthService, AuthService>();
			builder.Services.AddScoped<ITokenService, TokenService>();
			builder.Services.AddScoped<IOtpService, OtpService>();
			builder.Services.AddScoped<IEmailService, EmailService>();
			builder.Services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
			builder.Services.AddScoped<IFacebookOAuthService, FacebookOAuthService>();

			// Storage & Content Services
			builder.Services.AddSingleton<IStorageService, CloudinaryService>();

			// Core Moderation Engine
			var moderationConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ModerationConfig");
			builder.Services.AddSingleton<IBlacklistProvider>(new BlacklistProvider(moderationConfigPath));
			builder.Services.AddScoped<IModerationService, ModerationService>();
			
			// AI & Chapter Processing
			builder.Services.AddScoped<IAiModerationClient, AiModerationClient>();
			
			builder.Services.AddScoped<IChapterPageService, ChapterPageService>();
			// Translation Team Services
			builder.Services.AddScoped<ITranslationTeamService, TranslationTeamService>();
			builder.Services.AddScoped<ITranslationService, TranslationService>();
			
			builder.Services.AddScoped<ITranslationPermissionService, TranslationPermissionService>();
            
            // OCR Services
            builder.Services.AddScoped<IOCRService, TesseractOCRService>();


			//enable jwt token
			var _authkey = builder.Configuration.GetValue<string>("JwtSettings:securitykey");
			builder.Services.AddAuthentication(item =>
			{
				item.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
				item.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
			}).AddJwtBearer(item =>
			{
				item.RequireHttpsMetadata = true;
				item.SaveToken = true;
				item.TokenValidationParameters = new TokenValidationParameters()
				{
					ValidateIssuerSigningKey = true,
					IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_authkey)),
					ValidateIssuer = false,
					ValidateAudience = false,
					ValidateLifetime = true,
					ClockSkew = TimeSpan.Zero
				};

			});

			builder.Services.AddCors(options =>
      {
        options.AddPolicy("AllowSpecificOrigin", policy =>
              {
            policy
                  .WithOrigins("http://localhost:5173")
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
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MlndexDbContext>();
                db.Database.Migrate();
            }
            app.Run();
        }
    }
}
