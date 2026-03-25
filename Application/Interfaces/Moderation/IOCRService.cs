using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Moderation
{
  public interface IOCRService
  {
    // Nhận vào byte array của ảnh và trả về văn bản nhận diện được
    Task<string> ExtractTextFromImageAsync(byte[] imageBytes);
    
    // Mới: Trả về danh sách các vùng văn bản kèm tọa độ
    Task<List<Application.Models.OCR.OCRRegion>> ExtractTextRegionsFromImageAsync(byte[] imageBytes);
  }
}
