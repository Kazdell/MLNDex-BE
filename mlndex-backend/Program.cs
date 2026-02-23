using Microsoft.EntityFrameworkCore;
using mlndex_backend.Extension;

namespace mlndex_backend
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			var PORT = Environment.GetEnvironmentVariable("PORT") ?? "8888";

			if(builder.Environment.IsProduction())
			{
				builder.WebHost.UseUrls($"http://0.0.0.0:{PORT}");
			}
			else
			{
				builder.WebHost.UseUrls($"http://localhost:{PORT}");
			}

				// Add services to the container.

				builder.Services.AddControllers();
			// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddSwaggerGen();

			//builder.Services.AddDbContext<>(options =>
				//options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

			builder.Services.AddHttpClient();

			// Add Service vao day
			//builder.Services.AddScoped<Interfaces, Services>();
			// ============================

			// Add SignalR
			builder.Services.AddSignalR();

			builder.Services.AddCors(options =>
			{
				options.AddPolicy("AllowSpecificOrigin", policy =>
				{
					policy
					// Only allow these origins
					.WithOrigins("http://localhost:3000", "https://learnlinkk.vercel.app") // Thêm các origin khác ở đây
					.AllowAnyHeader()
					.AllowAnyMethod()
					.AllowCredentials();
				});
			});


			var app = builder.Build();

			// Configure the HTTP request pipeline.
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


			// Add cac HUB cua signalR vao day
			//app.MapHub<SignalRHub>("/signalrhub");

			app.MapControllers();

			app.Run();
		}
	}
}






