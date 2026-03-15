using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
	public class Chapter
	{
		public int ChapterId { get; set; }
		public int SeriesId { get; set; }
		public int? TeamId { get; set; }  // NULL = original, NOT NULL = translated
		public float ChapterNumber { get; set; }
		public string? Title { get; set; }
		public ContentType ContentType { get; set; }
		public int? PageCount { get; set; }
		public int? WordCount { get; set; }
		public ChapterLockStatus LockStatus { get; set; }
		public int? UnlockPriceCoins { get; set; }
		public DateTime? UnlockTime { get; set; }
		public ChapterStatus Status { get; set; }
		public ModerationStatus ModerationStatus { get; set; }
        public string? AiScoresJson { get; set; }
        public DateTime? PublishedAt { get; set; }
		public int Views { get; set; } = 0;

		// Navigation
		public Series Series { get; set; } = null!;
		public TranslationTeam? Team { get; set; }
		public ChapterText? ChapterText { get; set; }
		public ICollection<ChapterPage> Pages { get; set; } = new List<ChapterPage>();
		public ICollection<Translation> Translations { get; set; } = new List<Translation>();
		public ICollection<ChapterUnlock> ChapterUnlocks { get; set; } = new List<ChapterUnlock>();
		public ICollection<ReadingHistory> ReadingHistories { get; set; } = new List<ReadingHistory>();
		public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
	}

	public enum ContentType
	{
		IMAGE,
		TEXT
	}

	public enum ChapterLockStatus
	{
		FREE,
		COIN_LOCK,
		TIMED_LOCK
	}

	public enum ChapterStatus
	{
		DRAFT,
		PENDING,
		PUBLISHED,
		UNPUBLISHED
	}
}
