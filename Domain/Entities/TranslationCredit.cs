using System;
using System.Collections.Generic;

namespace Domain.Entities
{
  public class TranslationCredit
  {
    public int TranslationId { get; set; }
    public int UserId { get; set; }
    public TranslationRole Role { get; set; }

    // Navigation
    public Translation Translation { get; set; } = null!;
    public User User { get; set; } = null!;
  }

  public enum TranslationRole
  {
    TRANSLATOR,
    CLEANER,
    REDRAWER,
    TYPESETTER,
    PROOFREADER
  }
}
