using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Application.Interfaces.OCR
{
  public interface ITextDetectorService
  {
    /// <summary>
    /// Reads an image stream and runs inference (e.g. CRAFT ONNX model) to find text bounding boxes
    /// </summary>
    Task<List<BoundingBoxDto>> DetectTextBoxesAsync(Stream imageStream);
  }
}
