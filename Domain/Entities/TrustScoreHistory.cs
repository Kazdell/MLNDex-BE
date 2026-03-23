using System;

namespace Domain.Entities
{
  public class TrustScoreHistory
  {
    public int Id { get; set; }

    // Nullable foreign keys for polymorphic relationship
    public int? UserId { get; set; }
    public User? User { get; set; }

    public int? TranslationTeamId { get; set; }
    public TranslationTeam? TranslationTeam { get; set; }

    public int ScoreChange { get; set; } // VD: -50 (Trừ), +10 (Cộng)
    public string Reason { get; set; } = string.Empty; // Lý do xử lý (được Mod note lại)

    // Khóa ngoại liên kết tới Report nếu thay đổi xuất phát từ việc xử lý báo cáo
    public int? RelatedReportId { get; set; }
    public Report? RelatedReport { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  }
}
