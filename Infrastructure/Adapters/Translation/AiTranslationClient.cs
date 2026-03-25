using Application.Interfaces.Translation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Infrastructure.Adapters.Translation
{
  public class AiTranslationClient : IAiTranslationClient
  {
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiTranslationClient> _logger;

    public AiTranslationClient(IConfiguration configuration, ILogger<AiTranslationClient> logger)
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

    public async Task<List<string>> TranslateTextsAsync(List<string> texts, string targetLanguage = "Vietnamese", string context = "")
    {
      if (texts == null || texts.Count == 0) return new List<string>();

      var systemPrompt = $"You are an expert manga/comic translator. Translate the given text blocks into {targetLanguage}. Maintain the tone, style, and correct contextual meaning. Context: {context}. Respond strictly in JSON format returning an array of translated strings under the key 'translations' matching the exact order and length of the inputs.";
      
      var userContentJson = JsonSerializer.Serialize(new { texts });

      var requestBody = new
      {
        model = "gpt-4o-mini", // Cost-effective model for text translation
        response_format = new { type = "json_object" },
        messages = new[]
        {
          new { role = "system", content = systemPrompt },
          new { role = "user", content = userContentJson }
        },
        temperature = 0.3
      };

      var json = JsonSerializer.Serialize(requestBody);
      var content = new StringContent(json, Encoding.UTF8, "application/json");

      var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

      if (!response.IsSuccessStatusCode)
      {
        var error = await response.Content.ReadAsStringAsync();
        _logger.LogError("OpenAI Translation API lỗi {StatusCode}: {Error}", response.StatusCode, error);
        throw new Exception($"OpenAI Translation API error: {response.StatusCode}");
      }

      var responseBody = await response.Content.ReadAsStringAsync();
      try
      {
        using var doc = JsonDocument.Parse(responseBody);
        var choices = doc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() > 0)
        {
          var messageContent = choices[0].GetProperty("message").GetProperty("content").GetString();
          if (!string.IsNullOrEmpty(messageContent))
          {
            using var resultDoc = JsonDocument.Parse(messageContent);
            if (resultDoc.RootElement.TryGetProperty("translations", out var translationsArray) && translationsArray.ValueKind == JsonValueKind.Array)
            {
              var translatedList = new List<string>();
              foreach (var item in translationsArray.EnumerateArray())
              {
                translatedList.Add(item.GetString() ?? "");
              }
              
              if (translatedList.Count == texts.Count)
              {
                return translatedList;
              }
              else
              {
                _logger.LogWarning("Translation array length mismatch: Expected {Expected}, Got {Got}", texts.Count, translatedList.Count);
                // Fallback: fill with empty or original
                while (translatedList.Count < texts.Count) translatedList.Add("");
                return translatedList;
              }
            }
          }
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Lỗi parse JSON từ OpenAI Translation");
      }

      // Fallback in case of parsing failure
      var fallbackList = new List<string>();
      foreach (var t in texts) fallbackList.Add(t);
      return fallbackList;
    }
  }
}
