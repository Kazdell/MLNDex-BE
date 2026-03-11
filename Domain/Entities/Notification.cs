using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
	public class Notification
	{
		public int NotificationId { get; set; }
		public int UserId { get; set; }
		public NotificationType NotificationType { get; set; }
		public string Title { get; set; } = null!;
		public string Message { get; set; } = null!;
		public string ActionUrl { get; set; } = null!;
		public int? RelatedEntityId { get; set; }
		public string? RelatedEntityType { get; set; }
		public bool IsRead { get; set; }
		public DateTime CreatedAt { get; set; }

		// Navigation
		public User User { get; set; } = null!;
	}

	public enum NotificationType
	{
		NEW_SERIES,
		NEW_CHAPTER,
		CONTENT_APPROVED,
		CONTENT_REJECTED,
		CONTENT_BANNED,
		TEAM_LOCKED,
		TEAM_MONETIZATION,
		TRANSLATION_REQUEST,
		TRANSLATION_GRANTED,
		TRANSLATION_REVOKED,
		MOD_NEW_CONTENT,
		MOD_NEW_REPORT,
		REPORT_RESOLVED,
		COMMENT_REPLY,
		SYSTEM
	}
}
