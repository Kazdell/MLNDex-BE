using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
  public class TranslationText
  {
    public int TransTextId { get; set; }
    public int TranslationId { get; set; }  // Unique constraint (1-1 with Translation)
    public string ContentUrl { get; set; } = null!;
    public int WordCount { get; set; }

    // Navigation
    public Translation Translation { get; set; } = null!;
  }
}
