using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
	public class ModerationQueue
	{
		public int QueueId { get; set; }
		public int ContentId { get; set; }
		public ModerationQueueContentType ContentType { get; set; }
		public QueuePriority Priority { get; set; }
		public int? AssignedTo { get; set; }
		public QueueStatus Status { get; set; }
		public int ReportCount { get; set; }
		public DateTime FlaggedAt { get; set; }
		public DateTime? AssignedAt { get; set; }


		// ── Appeal (tác giả yêu cầu review lại) ────────
		public string? AppealReason { get; set; }         // Tác giả giải thích lý do
		public int AppealCount { get; set; } = 0;         // Đã appeal mấy lần

		// Navigation
		public User? AssignedModerator { get; set; }
		public ICollection<Report> Reports { get; set; } = new List<Report>();
		public ICollection<ModerationAction> ModerationActions { get; set; } = new List<ModerationAction>();
	}

	public enum ModerationQueueContentType
	{
		SERIES,
		CHAPTER,
		TRANSLATION
	}

	public enum QueuePriority
	{
		LOW,
		MEDIUM,
		HIGH,
		URGENT
	}

	public enum QueueStatus
	{
		PENDING,
		IN_REVIEW,
		RESOLVED,
		DISMISSED
	}

}
