using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities
{
    public class ReputationHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int? CreatorId { get; set; }
        public int? TranslationTeamId { get; set; }

        public int ScoreChange { get; set; }

        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public int? RelatedReportId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties (nullable — may be null depending on which target)
        public virtual CreatorProfile? Creator { get; set; }
        public virtual TranslationTeam? TranslationTeam { get; set; }
        public virtual Report? RelatedReport { get; set; }
    }
}
