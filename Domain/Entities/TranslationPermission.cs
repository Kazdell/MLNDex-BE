using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
	public class TranslationPermission
	{
		public int PermissionId { get; set; }
		public int SeriesId { get; set; }
		public int TeamId { get; set; }
		public int GrantedBy { get; set; }
		public TranslationPermissionStatus Status { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? GrantedAt { get; set; }
		public DateTime? RevokedAt { get; set; }
		public string? Note { get; set; }

		// Navigation
		public Series Series { get; set; } = null!;
		public TranslationTeam Team { get; set; } = null!;
		public User GrantedByUser { get; set; } = null!;
		public ICollection<Translation> Translations { get; set; } = new List<Translation>();
	}

	public enum TranslationPermissionStatus
	{
		PENDING,
		GRANTED,
		DENIED,
		REVOKED
	}
}
