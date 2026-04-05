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

    public async Task<List<Application.DTOs.User.OverlayTranslationResponse>> TranslatePageByAiVisionAsync(string base64Image, string sourceLanguage, string targetLanguage)
    {
      if (string.IsNullOrEmpty(base64Image)) return new List<Application.DTOs.User.OverlayTranslationResponse>();

      var systemPrompt = $@"You are an expert manga/comic translator and layout analyzer. 
Analyze the provided manga page image. 
Your task is to identify all speech bubbles, boxes, or floating texts.
For each text block:
1. Extract the original text in {sourceLanguage}. STRICTLY IGNORE any furigana (tiny ruby characters next to Kanji in Japanese). Only read the primary text.
2. Translate the text into {targetLanguage}. Maintain tone, style, and natural flow.
3. Determine the approximate bounding box of the text. Represent coordinates in percentages (0.00 to 100.00) relative to the image's total width and height.
   - X: Left edge %
   - Y: Top edge %
   - Width: Box width %
   - Height: Box height %

Return exactly a JSON object containing an array named 'regions'. Each region must have:
{{
  ""originalText"": ""string"",
  ""translatedText"": ""string"",
  ""x"": double,
  ""y"": double,
  ""width"": double,
  ""height"": double
}}";

      string imagePayload = "data:image/jpeg;base64," + base64Image;
      if (base64Image.StartsWith("data:image"))
      {
        imagePayload = base64Image;
      }

      var requestBody = new
      {
        model = "gpt-4o-mini", // Supports vision and is cost-effective
        response_format = new { type = "json_object" },
        messages = new object[]
        {
          new { role = "system", content = systemPrompt },
          new {
            role = "user",
            content = new object[]
            {
               new { type = "text", text = "Extract and translate the text on this page." },
               new { type = "image_url", image_url = new { url = imagePayload } }
            }
          }
        },
        temperature = 0.2
      };

      var json = JsonSerializer.Serialize(requestBody);
      var content = new StringContent(json, Encoding.UTF8, "application/json");

      var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

      if (!response.IsSuccessStatusCode)
      {
        var error = await response.Content.ReadAsStringAsync();
        _logger.LogError("OpenAI Vision API lỗi {StatusCode}: {Error}", response.StatusCode, error);
        throw new Exception($"OpenAI Vision API error: {response.StatusCode}");
      }

      var responseBody = await response.Content.ReadAsStringAsync();
      var resultRegions = new List<Application.DTOs.User.OverlayTranslationResponse>();

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
            if (resultDoc.RootElement.TryGetProperty("regions", out var regionsArray) && regionsArray.ValueKind == JsonValueKind.Array)
            {
              foreach (var region in regionsArray.EnumerateArray())
              {
                resultRegions.Add(new Application.DTOs.User.OverlayTranslationResponse
                {
                  OriginalText = region.TryGetProperty("originalText", out var dO) ? dO.GetString() : "",
                  TranslatedText = region.TryGetProperty("translatedText", out var dT) ? dT.GetString() : "",
                  X = region.TryGetProperty("x", out var dx) ? dx.GetDouble() : 0,
                  Y = region.TryGetProperty("y", out var dy) ? dy.GetDouble() : 0,
                  Width = region.TryGetProperty("width", out var dw) ? dw.GetDouble() : 0,
                  Height = region.TryGetProperty("height", out var dh) ? dh.GetDouble() : 0,
                  IsUserAdjusted = false,
                  Provider = "OpenAI_Vision"
                });
              }
            }
          }
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Lỗi parse JSON từ OpenAI Vision API");
      }

      return resultRegions;
    }
  }
}
