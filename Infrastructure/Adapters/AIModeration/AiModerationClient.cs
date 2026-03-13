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

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
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
                            maxScores[kvp.Key] = kvp.Value;
                    }
                    if (result.Flagged) overallFlagged = true;
                }
                catch (Exception ex)
                {
                    var safeUrl = url.Length > 100 ? url[..100] + "..." : url;
                    _logger.LogError(ex, "Lỗi khi kiểm duyệt ảnh: {Url}", safeUrl);
                }
            }

            return new AiModerationResultDto
            {
                CategoryScores = maxScores,
                Flagged = overallFlagged,
                FlaggedReason = string.Join(", ", maxScores
                    .Where(s => s.Value >= 0.1)
                    .OrderByDescending(s => s.Value)
                    .Select(s => s.Key))
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

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync("https://api.openai.com/v1/moderations", content);
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("OpenAI API timeout khi kiểm duyệt ảnh");
                throw new TimeoutException("OpenAI moderation API timeout sau 60 giây");
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenAI API lỗi {StatusCode}: {Error}", response.StatusCode, error);

                if ((int)response.StatusCode == 429)
                    throw new InvalidOperationException("OpenAI rate limit — thử lại sau ít giây");

                response.EnsureSuccessStatusCode(); 
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            return ParseScoresAndFlag(responseBody);
        }

        private (Dictionary<string, double> Scores, bool Flagged) ParseScoresAndFlag(string responseJson)
        {
            var scores = new Dictionary<string, double>();
            bool flagged = false;

            try
            {
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
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Lỗi parse JSON từ OpenAI: {Json}",
                    responseJson.Length > 200 ? responseJson[..200] : responseJson);
            }

            return (scores, flagged);
        }
    }
}
