using System;

namespace Application.DTOs.Translation
{
  public class PageTextLayerDto
  {
    public int LayerId { get; set; }
    public int PageId { get; set; }

    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public string OriginalText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;

    public bool IsVerified { get; set; }
  }
}
