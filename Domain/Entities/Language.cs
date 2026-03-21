using System;
using System.Collections.Generic;

namespace Domain.Entities
{
	public class Language
	{
		public int LanguageId { get; set; }
		public string Code { get; set; } = null!;
		public string Name { get; set; } = null!;

		// Navigation properties
		public ICollection<TranslationTeam> TranslationTeams { get; set; } = new List<TranslationTeam>();
		public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
		public ICollection<Translation> Translations { get; set; } = new List<Translation>();
		public ICollection<TranslationPermission> TranslationPermissions { get; set; } = new List<TranslationPermission>();
	}
}
