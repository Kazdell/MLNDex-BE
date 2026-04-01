using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Application.Interfaces.OCR
{
    public interface IImagePreprocessorService
    {
        /// <summary>
        /// Crops regions from the original image based on given bounding boxes, 
        /// applies Grayscale and Thresholding to maximize contrast, and returns the small image streams.
        /// </summary>
        Task<List<Stream>> CutAndCleanBoxesAsync(Stream originalImageStream, List<BoundingBoxDto> boxes);
    }
}
