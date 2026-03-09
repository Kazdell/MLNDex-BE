namespace Application.DTOs.Moderation
{
    // Request for text-based content moderation.
    public class TextCheckRequest
    {
        public string Text { get; set; } = string.Empty;
        public bool IsComment { get; set; } = false;
        public int UserReputation { get; set; } = 100;
    }

    // Request for OpenAI score-based moderation.
    public class OpenAiScoreRequest
    {
        public Dictionary<string, double> Scores { get; set; } = new();
        public string TargetAgeRating { get; set; } = "ALL";
    }

    // Response from text moderation check.
    public class TextCheckResponse
    {
        public string Action { get; set; } = string.Empty;
        public List<string> Reasons { get; set; } = new();
        public int PenaltyPoints { get; set; }
        public string? TemplateId { get; set; }
        public bool IsPermaBan { get; set; }
    }

    // Response from OpenAI score analysis.
    public class OpenAiScoreResponse
    {
        public string Action { get; set; } = string.Empty;
        public string? WorstCategory { get; set; }
        public double? WorstScore { get; set; }
        public string? TemplateId { get; set; }
        public string? SuggestedAgeRating { get; set; }
        public bool IsPermaBan { get; set; }
        public int ReputationDeduction { get; set; }
    }

    public class RejectionTemplateDto
    {
        public string Id { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
    }

    public class BannedTagDto
    {
        public string Tag { get; set; } = string.Empty;
        public List<string> Variants { get; set; } = new();
        public string Severity { get; set; } = string.Empty;
        public string? Action { get; set; }
    }
}
