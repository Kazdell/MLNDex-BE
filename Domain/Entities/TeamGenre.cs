using System;
using System.Collections.Generic;

namespace Domain.Entities
{
  public class TeamGenre
  {
    public int TeamGenreId { get; set; }
    public int TeamId { get; set; }
    public int GenreId { get; set; }

    // Navigation
    public TranslationTeam TranslationTeam { get; set; } = null!;
    public Genre Genre { get; set; } = null!;
  }
}
