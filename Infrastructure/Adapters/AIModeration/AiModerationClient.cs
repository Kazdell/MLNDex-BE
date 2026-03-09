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

			var maxScores = new Dictionary<string, double>();
            bool overallFlagged = false;

			foreach (var url in urls)
			{
				try
				{
					var result = await ModerateOneImageWithFlagAsync(url);
					foreach (var kvp in result.Scores)
					{
						if (!maxScores.ContainsKey(kvp.Key) || kvp.Value > maxScores[kvp.Key])
						{
							maxScores[kvp.Key] = kvp.Value;
						}
					}
                    if (result.Flagged) overallFlagged = true;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Lỗi khi kiểm duyệt ảnh: {Url}", url);
				}
			}

			return new AiModerationResultDto
			{
				CategoryScores = maxScores,
				Flagged = overallFlagged,
				FlaggedReason = string.Join(", ", maxScores.Where(s => s.Value >= 0.1).Select(s => s.Key)) // Ghi lại các category có score đáng kể
			};
		}

		private async Task<(Dictionary<string, double> Scores, bool Flagged)> ModerateOneImageWithFlagAsync(string imageUrl)
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
            return ParseScoresAndFlag(responseBody);
		}

		private (Dictionary<string, double> Scores, bool Flagged) ParseScoresAndFlag(string responseJson)
		{
			var scores = new Dictionary<string, double>();
            bool flagged = false;

			using var doc = JsonDocument.Parse(responseJson);
			var results = doc.RootElement.GetProperty("results");
			if (results.GetArrayLength() == 0) return (scores, flagged);

            var firstResult = results[0];
            flagged = firstResult.GetProperty("flagged").GetBoolean();

			var categoryScores = firstResult.GetProperty("category_scores");
			foreach (var property in categoryScores.EnumerateObject())
			{
				scores[property.Name] = property.Value.GetDouble();
			}

			return (scores, flagged);
		}
	}
}