using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
  public class ChapterPage
  {
    public int PageId { get; set; }
    public int ChapterId { get; set; }
    public int PageNumber { get; set; }
    public string ImageUrl { get; set; } = null!;

    // Navigation
    public Chapter Chapter { get; set; } = null!;
  }
}
