using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
	public class ReadingHistory
	{
		public int HistoryId { get; set; }
		public int UserId { get; set; }
		public int SeriesId { get; set; }
		public int LastChapterId { get; set; }
		public int LastPageNumber { get; set; }
		public DateTime LastReadAt { get; set; }

		// Navigation
		public User User { get; set; } = null!;
		public Series Series { get; set; } = null!;
		public Chapter LastChapter { get; set; } = null!;
	}
}
