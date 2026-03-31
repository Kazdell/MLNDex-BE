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
    public class GeminiTranslationClient : IAiTranslationClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiTranslationClient> _logger;
        private readonly string _apiKey;

        public GeminiTranslationClient(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiTranslationClient> logger)
        {
            _apiKey = configuration["Gemini:ApiKey"]
                ?? throw new InvalidOperationException("Chưa cấu hình Gemini:ApiKey trong appsettings");

            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<string>> TranslateTextsAsync(List<string> texts, string targetLanguage = "Vietnamese", string context = "")
        {
            if (texts == null || texts.Count == 0) return new List<string>();

            var systemPrompt = $"You are an expert manga/comic translator. Translate the given text blocks into {targetLanguage}. Maintain the tone, style, and correct contextual meaning. Context: {context}. Respond strictly in JSON format returning an array of translated strings under the key 'translations' matching the exact order and length of the inputs.";
            var userText = JsonSerializer.Serialize(new { texts });

            var requestBody = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = userText } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.3,
                    responseMimeType = "application/json"
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Gemini Translation API lỗi {StatusCode}: {Error}", response.StatusCode, error);
                throw new Exception($"API Gemini trả về lỗi [{response.StatusCode}]: {error}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var textContent = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        using var resultDoc = JsonDocument.Parse(textContent);
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
                                while (translatedList.Count < texts.Count) translatedList.Add("");
                                return translatedList;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi parse JSON từ Gemini Translation");
            }

            var fallbackList = new List<string>();
            foreach (var t in texts) fallbackList.Add(t);
            return fallbackList;
        }

        public async Task<List<Application.DTOs.Translation.OverlayTranslationResponse>> TranslatePageByAiVisionAsync(string base64Image, string sourceLanguage, string targetLanguage)
        {
            if (string.IsNullOrEmpty(base64Image)) return new List<Application.DTOs.Translation.OverlayTranslationResponse>();

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

            string mimeType = "image/jpeg";
            if (string.IsNullOrEmpty(base64Image)) return new List<Application.DTOs.Translation.OverlayTranslationResponse>();

            string base64Data = base64Image;

            if (base64Image.StartsWith("data:image"))
            {
                var parts = base64Image.Split(',');
                if (parts.Length == 2)
                {
                    var header = parts[0];
                    base64Data = parts[1];
                    var match = System.Text.RegularExpressions.Regex.Match(header, @"data:(image/[a-zA-Z]+);base64");
                    if (match.Success)
                    {
                        mimeType = match.Groups[1].Value;
                    }
                }
            }

            base64Data = base64Data.Replace("\n", "").Replace("\r", "").Replace(" ", "");

            var requestBody = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new { text = "Extract and translate the text on this page." },
                            new { inlineData = new { mimeType = mimeType, data = base64Data } }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.2,
                    responseMimeType = "application/json"
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Use gemini-1.5-pro for vision tasks, or gemini-1.5-flash
            var response = await _httpClient.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro:generateContent?key={_apiKey}", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Gemini Vision API lỗi {StatusCode}: {Error}", response.StatusCode, error);
                throw new Exception($"API Gemini (Vision) trả về lỗi [{response.StatusCode}]: {error}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var resultRegions = new List<Application.DTOs.Translation.OverlayTranslationResponse>();

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var textContent = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        using var resultDoc = JsonDocument.Parse(textContent);
                        if (resultDoc.RootElement.TryGetProperty("regions", out var regionsArray) && regionsArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var region in regionsArray.EnumerateArray())
                            {
                                resultRegions.Add(new Application.DTOs.Translation.OverlayTranslationResponse
                                {
                                    OriginalText = region.TryGetProperty("originalText", out var dO) ? dO.GetString() : "",
                                    TranslatedText = region.TryGetProperty("translatedText", out var dT) ? dT.GetString() : "",
                                    X = region.TryGetProperty("x", out var dx) ? dx.GetDouble() : 0,
                                    Y = region.TryGetProperty("y", out var dy) ? dy.GetDouble() : 0,
                                    Width = region.TryGetProperty("width", out var dw) ? dw.GetDouble() : 0,
                                    Height = region.TryGetProperty("height", out var dh) ? dh.GetDouble() : 0,
                                    IsUserAdjusted = false,
                                    Provider = "Gemini_Vision"
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi parse JSON từ Gemini Vision API: {ResponseBody}", responseBody);
                throw new Exception($"Lỗi đọc JSON từ Gemini: {ex.Message}. ResponseBody: {responseBody}");
            }

            return resultRegions;
        }
    }
}
