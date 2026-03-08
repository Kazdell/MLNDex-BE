using Application.DTOs.AIModeration;
using Application.Interfaces.AIModeration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Adapters.AIModeration
{
	public class AiModerationClient : IAiModerationClient
	{
		private readonly HttpClient _httpClient;
		private readonly ILogger<AiModerationClient> _logger;

		public AiModerationClient(IConfiguration configuration, ILogger<AiModerationClient> logger)
		{
			var apiKey = configuration["OpenAI:ApiKey"]
				?? throw new InvalidOperationException("Chưa cấu hình OpenAI:ApiKey trong appsettings");

			_httpClient = new HttpClient();
			_httpClient.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("Bearer", apiKey);

			_logger = logger;
		}

		public async Task<AiModerationResultDto> ModerateImagesAsync(IEnumerable<string> imageUrls)
		{
			var urls = imageUrls.ToList();
			_logger.LogInformation("Bắt đầu AI kiểm duyệt {Count} ảnh", urls.Count);

			var allFlaggedCategories = new List<string>();

			foreach (var url in urls)
			{
				try
				{
					var flaggedCategories = await ModerateOneImageAsync(url);
					allFlaggedCategories.AddRange(flaggedCategories);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Lỗi khi kiểm duyệt ảnh: {Url}", url);
				}
			}

			var flagged = allFlaggedCategories.Count > 0;

			return new AiModerationResultDto
			{
				Flagged = flagged,
				FlaggedReason = flagged
					? string.Join(", ", allFlaggedCategories.Distinct())
					: null
			};
		}

		private async Task<List<string>> ModerateOneImageAsync(string imageUrl)
		{
			var requestBody = new
			{
				model = "omni-moderation-latest",
				input = new[]
				{
					new
					{
						type      = "image_url",
						image_url = new { url = imageUrl }
					}
				}
			};

			var json = JsonSerializer.Serialize(requestBody);
			var content = new StringContent(json, Encoding.UTF8, "application/json");
			var response = await _httpClient.PostAsync("https://api.openai.com/v1/moderations", content);

			if (!response.IsSuccessStatusCode)
			{
				var error = await response.Content.ReadAsStringAsync();
				_logger.LogError("OpenAI API lỗi {StatusCode}: {Error}", response.StatusCode, error);
				response.EnsureSuccessStatusCode();
			}

			var responseBody = await response.Content.ReadAsStringAsync();
			return ParseFlaggedCategories(responseBody, imageUrl);
		}

		private List<string> ParseFlaggedCategories(string responseJson, string imageUrl)
		{
			var categories = new List<string>();

			using var doc = JsonDocument.Parse(responseJson);
			var results = doc.RootElement.GetProperty("results");
			var firstResult = results[0];

			// Dùng trực tiếp flagged từ OpenAI thay vì tự tính threshold
			var flagged = firstResult.GetProperty("flagged").GetBoolean();
			if (!flagged) return categories;

			// Lấy các category OpenAI đánh dấu là true
			var categoryFlags = firstResult.GetProperty("categories");
			foreach (var category in categoryFlags.EnumerateObject())
			{
				if (category.Value.GetBoolean())
					categories.Add(category.Name);
			}

			// Nếu OpenAI flag nhưng không có category cụ thể → ghi "unknown"
			if (categories.Count == 0)
				categories.Add("unknown");

			_logger.LogWarning("Ảnh bị flag: {Url} | Lý do: {Reason}",
				imageUrl, string.Join(", ", categories));

			return categories;
		}
	}
}