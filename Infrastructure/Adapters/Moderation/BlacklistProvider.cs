using System.Text.Json;
using Application.DTOs.Moderation;
using Application.Interfaces.Moderation;

namespace Infrastructure.Adapters.Moderation
{
  // Loads and caches moderation config files from disk.
  public class BlacklistProvider : IBlacklistProvider
  {
    private readonly string _configPath;
    private List<string> _dynamicBlacklist = new();

    public List<BlacklistEntry> ProfanityList { get; private set; } = new();
    public List<BlacklistEntry> HateSpeechList { get; private set; } = new();
    public List<BlacklistEntry> IllegalContentList { get; private set; } = new();
    public List<RejectionTemplateDto> RejectionTemplates { get; private set; } = new();
    public List<BannedTagDto> BannedTags { get; private set; } = new();
    public List<BannedTagDto> RestrictedTags { get; private set; } = new();
    public Dictionary<string, ThresholdRule> Thresholds { get; private set; } = new();

    public BlacklistProvider(string configPath)
    {
      _configPath = configPath;
      LoadAll();
    }

    public void LoadAll()
    {
      LoadBlacklist();
      LoadRejectionTemplates();
      LoadBannedTags();
      LoadThresholds();
    }

    private void LoadBlacklist()
    {
      var path = Path.Combine(_configPath, "blacklist.json");
      if (!File.Exists(path)) return;

      var json = File.ReadAllText(path);
      var data = JsonSerializer.Deserialize<BlacklistFile>(json, new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true
      });

      if (data != null)
      {
        ProfanityList = data.Profanity ?? new();
        HateSpeechList = data.HateSpeech ?? new();
        IllegalContentList = data.IllegalContent ?? new();
      }
    }

    private void LoadRejectionTemplates()
    {
      var path = Path.Combine(_configPath, "rejection_templates.json");
      if (!File.Exists(path)) return;

      var json = File.ReadAllText(path);
      RejectionTemplates = JsonSerializer.Deserialize<List<RejectionTemplateDto>>(json, new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true
      }) ?? new();
    }

    private void LoadBannedTags()
    {
      var path = Path.Combine(_configPath, "banned_tags.json");
      if (!File.Exists(path)) return;

      var json = File.ReadAllText(path);
      var data = JsonSerializer.Deserialize<BannedTagsFile>(json, new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true
      });

      if (data != null)
      {
        BannedTags = data.BannedTags ?? new();
        RestrictedTags = data.RestrictedTags ?? new();
      }
    }

    private void LoadThresholds()
    {
      var path = Path.Combine(_configPath, "threshold_config.json");
      if (!File.Exists(path)) return;

      var json = File.ReadAllText(path);
      var data = JsonSerializer.Deserialize<ThresholdConfigFile>(json, new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true
      });

      if (data?.Thresholds != null)
        Thresholds = data.Thresholds;
    }

    public void SetDynamicBlacklist(List<string> words)
    {
      _dynamicBlacklist = words ?? new();
      // We merge dynamic words into ProfanityList for simplicity in this implementation
      // or we can handle them separately in the moderation logic.
    }

    public async Task AddBlacklistWordAsync(string word, string category, string severity)
    {
      var path = Path.Combine(_configPath, "blacklist.json");
      BlacklistFile data = new BlacklistFile { Profanity = new(), HateSpeech = new(), IllegalContent = new() };
      
      if (File.Exists(path))
      {
        var json = await File.ReadAllTextAsync(path);
        data = JsonSerializer.Deserialize<BlacklistFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? data;
      }

      data.Profanity ??= new();
      data.HateSpeech ??= new();
      data.IllegalContent ??= new();

      // Check if word already exists to prevent duplicates
      bool wordExists = data.Profanity.Any(x => x.Word.Equals(word, StringComparison.OrdinalIgnoreCase)) ||
                        data.HateSpeech.Any(x => x.Word.Equals(word, StringComparison.OrdinalIgnoreCase)) ||
                        data.IllegalContent.Any(x => x.Word.Equals(word, StringComparison.OrdinalIgnoreCase));

      if (!wordExists)
      {
        var newEntry = new BlacklistEntry { Word = word, Severity = severity, Variants = new List<string>() };

        switch (category?.ToLower())
        {
          case "hatespeech":
            data.HateSpeech.Add(newEntry);
            break;
          case "illegalcontent":
            data.IllegalContent.Add(newEntry);
            break;
          case "profanity":
          default:
            data.Profanity.Add(newEntry);
            break;
        }

        var newJson = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, newJson);
        LoadBlacklist();
      }
    }

    public async Task<string> GetBlacklistJsonAsync()
    {
      var path = Path.Combine(_configPath, "blacklist.json");
      if (File.Exists(path))
      {
        return await File.ReadAllTextAsync(path);
      }
      return "{}";
    }

    public async Task UpdateThresholdsAsync(Dictionary<string, ThresholdRule> thresholds)
    {
      var path = Path.Combine(_configPath, "threshold_config.json");
      if (File.Exists(path))
      {
        var json = await File.ReadAllTextAsync(path);
        var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(json) as System.Text.Json.Nodes.JsonObject;
        if (jsonNode != null)
        {
          // Check if "thresholds" exists with lowercase, otherwise use "Thresholds"
          string key = jsonNode.ContainsKey("thresholds") ? "thresholds" : "Thresholds";
          jsonNode[key] = JsonSerializer.SerializeToNode(thresholds);
          
          var newJson = jsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
          await File.WriteAllTextAsync(path, newJson);

          // Update source file so changes persist across builds
          try
          {
            var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
            while (currentDir != null)
            {
                var sourcePath = Path.Combine(currentDir.FullName, "Infrastructure", "ModerationConfig", "threshold_config.json");
                if (File.Exists(sourcePath))
                {
                    await File.WriteAllTextAsync(sourcePath, newJson);
                    break;
                }
                currentDir = currentDir.Parent;
            }
          }
          catch (Exception)
          {
            // Ignore path errors in production/other environments
          }
        }
      }
      LoadThresholds();
    }

    public async Task<Dictionary<string, ThresholdRule>> GetThresholdsAsync()
    {
      return await Task.FromResult(Thresholds);
    }

    // New helper to get a combined list if needed, or we just expect the consumer to check both.
    // In this project's ModerationService, it iterates over ProfanityList, HateSpeechList, etc.
    // Let's modify LoadBlacklist to clear and reload, then we can append dynamic words.

    // Deserialization models
    public class BlacklistFile
    {
      public List<BlacklistEntry>? Profanity { get; set; }
      public List<BlacklistEntry>? HateSpeech { get; set; }
      public List<BlacklistEntry>? IllegalContent { get; set; }
    }

    public class BannedTagsFile
    {
      public List<BannedTagDto>? BannedTags { get; set; }
      public List<BannedTagDto>? RestrictedTags { get; set; }
    }

    public class ThresholdConfigFile
    {
      public Dictionary<string, ThresholdRule>? Thresholds { get; set; }
    }
  }
}
