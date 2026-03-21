using System;

namespace Domain.Entities
{
    public class Appeal
    {
        public int AppealId { get; set; }
        public int UserId { get; set; }
        public int? RelatedReportId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? EvidenceUrl { get; set; }
        public AppealStatus Status { get; set; } = AppealStatus.Pending;
        public int? ReviewedBy { get; set; }
        public string? ReviewNotes { get; set; }
        public int? ScoreRestored { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }

        // Navigation
        public User User { get; set; } = null!;
        public Report? RelatedReport { get; set; }
        public User? Reviewer { get; set; }
    }

    public enum AppealStatus
    {
        Pending,
        Approved,
        Rejected
    }
}
