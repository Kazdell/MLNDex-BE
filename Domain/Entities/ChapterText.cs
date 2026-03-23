using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
  public class ChapterText
  {
    public int TextId { get; set; }
    public int ChapterId { get; set; }  // Unique constraint (1-1 with Chapter)
    public string ContentUrl { get; set; } = null!;
    public int WordCount { get; set; }

    // Navigation
    public Chapter Chapter { get; set; } = null!;
  }
}
