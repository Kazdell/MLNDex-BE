using System;

namespace Application.Models.OCR
{
    public class OCRRegion
    {
        public string Text { get; set; } = string.Empty;
        
        // Coordinates (pixels initially, frontend/API might convert to percentages)
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
