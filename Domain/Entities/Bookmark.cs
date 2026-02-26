using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
	public class Bookmark
	{
		public int BookmarkId { get; set; }
		public int UserId { get; set; }
		public int SeriesId { get; set; }
		public int? ChapterId { get; set; }  // NULL = bookmark series, NOT NULL = bookmark chapter
		public DateTime BookmarkedAt { get; set; }
		public string? Note { get; set; }

		// Navigation
		public User User { get; set; } = null!;
		public Series Series { get; set; } = null!;
		public Chapter? Chapter { get; set; }
	}
}
