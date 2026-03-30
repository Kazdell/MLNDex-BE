using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Application.Interfaces.Translation
{
    public class OcrResultDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public interface IOcrService
    {
        Task<List<OcrResultDto>> ExtractTextAsync(Stream imageStream);
    }
}
