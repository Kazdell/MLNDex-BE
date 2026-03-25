using System;

namespace Domain.Entities
{
  public class PageTextLayer
  {
    public int LayerId { get; set; }
    public int PageId { get; set; }

    // Bounding Box coordinates (percentage relative to image width/height)
    // For Tesseract, we might get absolute pixels, but backend should convert to % 
    // before saving to make frontend responsive.
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public string OriginalText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public bool IsVerified { get; set; }

    // Navigation
    public ChapterPage Page { get; set; } = null!;
  }
}
