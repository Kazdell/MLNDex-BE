using System;
using System.Collections.Generic;

namespace Domain.Entities
{
  public class TranslationTeamJoin
  {
    public int TranslationId { get; set; }
    public int TeamId { get; set; }
    public bool IsPrimary { get; set; } = false;

    // Navigation
    public Translation Translation { get; set; } = null!;
    public TranslationTeam Team { get; set; } = null!;
  }
}
