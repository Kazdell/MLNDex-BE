using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Enums;

namespace Domain.Entities
{
	  public class Report
	  {
		public int ReportId { get; set; }
		public int ReporterId { get; set; }
		public int ContentId { get; set; }
		public ReportTargetType ContentType { get; set; }
		public ReportReason Reason { get; set; }
		public string? Description { get; set; }
		public string EvidenceUrlsJson { get; set; } = "[]";
		public ReportStatus Status { get; set; } = ReportStatus.Pending;
		public int? QueueId { get; set; }
		public DateTime CreatedAt { get; set; }

		// Navigation
		public User Reporter { get; set; } = null!;
		public ModerationQueue Queue { get; set; } = null!;
	  }

	public enum ReportTargetType
	{
		Series,
		ChapterTranslation,
		Team,
		User
	}

	public enum ReportReason
	{
		Plagiarism,
		MTL_Abuse,
		Unauthorized_Reup,
		Inappropriate,
		Other
	}

	public enum ReportStatus
	{
		Pending,
		Investigating,
		Resolved,
		Rejected
	}
}
