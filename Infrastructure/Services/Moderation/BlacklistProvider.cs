using System.Text.Json;
using Application.DTOs.Moderation;

namespace Infrastructure.Services.Moderation
{
    /// <summary>
    /// Loads and caches moderation config files (blacklist, templates, banned tags, thresholds).
    /// Files are read once at startup and cached in memory for high performance.
    /// </summary>
    public class BlacklistProvider
    {
        private readonly string _configPath;

        // Cached data
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

        /// <summary>
        /// Load or reload all configuration files from disk.
        /// </summary>
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
            {
                Thresholds = data.Thresholds;
            }
        }

        // Internal deserialization models
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

    /// <summary>
    /// A single blacklist word entry with its variants and severity.
    /// </summary>
    public class BlacklistEntry
    {
        public string Word { get; set; } = string.Empty;
        public List<string> Variants { get; set; } = new();
        public string Severity { get; set; } = "low";
    }

    /// <summary>
    /// Threshold rules for a single OpenAI moderation category.
    /// </summary>
    public class ThresholdRule
    {
        public double AUTO_REJECT { get; set; }
        public double FLAG_FOR_REVIEW { get; set; }
    }
}
