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
    // Chapters không còn FK về Language nữa. Bỏ navigation để tránh EF sinh shadow FK 'LanguageId' trên Chapter.
    public ICollection<Translation> Translations { get; set; } = new List<Translation>();
    public ICollection<TranslationPermission> TranslationPermissions { get; set; } = new List<TranslationPermission>();
  }
}
