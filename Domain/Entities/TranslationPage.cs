using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
  public class TranslationPage
  {
    public int TransPageId { get; set; }
    public int TranslationId { get; set; }
    public int PageNumber { get; set; }
    public string TranslationImageUrl { get; set; } = null!;

    // Navigation
    public Translation Translation { get; set; } = null!;
  }
}
