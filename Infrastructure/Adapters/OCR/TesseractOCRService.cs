using Application.Interfaces.Moderation;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;

namespace Infrastructure.Adapters.Tesseract
{
    public class TesseractOCRService : IOCRService
    {
        private readonly string _dataPath; // Đường dẫn đến thư mục tessdata
        private readonly ILogger<TesseractOCRService> _logger;

        public TesseractOCRService(ILogger<TesseractOCRService> logger)
        {
            _logger = logger;
            // Lấy đường dẫn thư mục thực thi để tìm thư mục tessdata đi kèm project
            _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
        }

        public async Task<string> ExtractTextFromImageAsync(byte[] imageBytes)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var engine = new TesseractEngine(_dataPath, "vie+eng", EngineMode.Default);
                    using var img = Pix.LoadFromMemory(imageBytes);
                    using var page = engine.Process(img);

                    var text = page.GetText();
                    return string.IsNullOrWhiteSpace(text) ? string.Empty : text;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xử lý OCR với Tesseract.");
                    throw;
                }
            });
        }
    }
}
