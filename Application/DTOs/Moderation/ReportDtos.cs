using System.ComponentModel.DataAnnotations;
using Domain.Entities;

namespace Application.DTOs.Moderation
{
    public class CreateReportRequest
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int ContentId { get; set; }

        [Required]
        public ReportContentType ContentType { get; set; }

        [Required]
        public ReportReason Reason { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }
    }

    public class ReportDto
    {
        public int ReportId { get; set; }
        public int ContentId { get; set; }
        public ReportContentType ContentType { get; set; }
        public ReportReason Reason { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ModerationQueueDto
    {
        public int QueueId { get; set; }
        public int ContentId { get; set; }
        public ModerationQueueContentType ContentType { get; set; }
        public QueuePriority Priority { get; set; }
        public QueueStatus Status { get; set; }
        public int ReportCount { get; set; }
        public DateTime FlaggedAt { get; set; }

        public string? AppealReason { get; set; }

        // Mới thêm để hiển thị trên UI
        public string? ContentTitle { get; set; }
        public string? AuthorName { get; set; }

        // Thêm danh sách report để hiển thị chi tiết (bao gồm cả AI reports)
        public List<ReportDto> Reports { get; set; } = new();
    }

    public class ModerationQueueListResponse
    {
        public List<ModerationQueueDto> Items { get; set; } = new();
    }

    public class ModerationDecisionRequest
    {
        [Required]
        public QueueStatus Status { get; set; }

        [Required]
        [StringLength(300)]
        public string Reason { get; set; } = string.Empty;
    }
}
