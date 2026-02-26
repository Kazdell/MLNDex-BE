namespace Application.DTOs.Moderation
{
    /// <summary>
    /// Request payload for text-based content moderation.
    /// Used by both chapter text (Light Novel) and comment moderation.
    /// </summary>
    public class TextCheckRequest
    {
        /// <summary>The raw text content to moderate (from OCR or direct input).</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Whether this text is a comment (true) or story content (false). Comments have a higher tolerance threshold.</summary>
        public bool IsComment { get; set; } = false;

        /// <summary>Current reputation score of the user (0-100). Users below 50 receive harsher penalties.</summary>
        public int UserReputation { get; set; } = 100;
    }

    /// <summary>
    /// Request payload for OpenAI score-based moderation.
    /// Simulates receiving scores from the omni-moderation-latest API.
    /// </summary>
    public class OpenAiScoreRequest
    {
        /// <summary>
        /// Dictionary of category scores from OpenAI (0.0 - 1.0).
        /// Keys: "violence", "sexual", "sexual/minors", "hate", "hate/threatening", "self-harm", "harassment"
        /// </summary>
        public Dictionary<string, double> Scores { get; set; } = new();
    }

    /// <summary>
    /// Response payload containing the moderation decision.
    /// </summary>
    public class TextCheckResponse
    {
        public string Action { get; set; } = string.Empty;
        public List<string> Reasons { get; set; } = new();
        public int PenaltyPoints { get; set; }
        public string? TemplateId { get; set; }
        public bool IsPermaBan { get; set; }
    }

    /// <summary>
    /// Response payload for OpenAI score analysis.
    /// </summary>
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

    /// <summary>
    /// A single rejection template entry.
    /// </summary>
    public class RejectionTemplateDto
    {
        public string Id { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
    }

    /// <summary>
    /// A single banned/restricted tag entry.
    /// </summary>
    public class BannedTagDto
    {
        public string Tag { get; set; } = string.Empty;
        public List<string> Variants { get; set; } = new();
        public string Severity { get; set; } = string.Empty;
        public string? Action { get; set; }
    }
}
