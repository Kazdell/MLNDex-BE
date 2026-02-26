using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
	public class Report
	{
		public int ReportId { get; set; }
		public int ReporterId { get; set; }
		public int ContentId { get; set; }
		public ReportContentType ContentType { get; set; }
		public ReportReason Reason { get; set; }
		public string? Description { get; set; }
		public int QueueId { get; set; }
		public DateTime CreatedAt { get; set; }

		// Navigation
		public User Reporter { get; set; } = null!;
		public ModerationQueue Queue { get; set; } = null!;
	}

	public enum ReportContentType
	{
		SERIES,
		CHAPTER,
		COMMENT,
		TRANSLATION
	}

	public enum ReportReason
	{
		SPAM,
		INAPPROPRIATE,
		COPYRIGHT,
		WRONG_CATEGORY,
		OTHER
	}
}
