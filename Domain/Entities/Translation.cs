using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
	public class Translation
	{
		public int TranslationId { get; set; }
		public int ChapterId { get; set; }
		public int PermissionId { get; set; }
		public int LanguageId { get; set; } = 1;
		public ContentType ContentType { get; set; }
		public TranslationQualityStatus QualityStatus { get; set; }
		public ModerationStatus ModerationStatus { get; set; }
		public DateTime? PublishedAt { get; set; }

		// Navigation
		public Language Language { get; set; } = null!;
		public Chapter Chapter { get; set; } = null!;
		public TranslationPermission Permission { get; set; } = null!;
		public ICollection<TranslationPage> TranslationPages { get; set; } = new List<TranslationPage>();
		public TranslationText? TranslationText { get; set; }
	}

	public enum TranslationQualityStatus
	{
		DRAFT,
		REVIEWING,
		PUBLISHED
	}
}
