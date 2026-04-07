using System;

namespace Application.Models.OCR
{
  public class OCRRegion
  {
    public string Text { get; set; } = string.Empty;

    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
  }
}
