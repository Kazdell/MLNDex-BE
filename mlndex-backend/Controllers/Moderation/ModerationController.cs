using Application.DTOs.Common;
using Application.Exceptions;
using Application.DTOs.Moderation;
using Application.Interfaces.AIModeration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using mlndex_backend.Controllers;
using Application.Interfaces.Moderation;

namespace mlndex_backend.Controllers.Moderation
{
  [Route("api/[controller]")]
  [Authorize]
  public class ModerationController : BaseController
  {
    private readonly IModerationService _moderationService;
    private readonly IOCRService _ocr;

    public ModerationController(IModerationService moderationService, IOCRService ocr)
    {
      _moderationService = moderationService;
      _ocr = ocr;
    }

    // Check text against blacklist and profanity filter.
    [HttpPost("check-text")]
    public async Task<IActionResult> CheckText([FromBody] TextCheckRequest request)
    {
      if (string.IsNullOrWhiteSpace(request.Text))
        throw new AppException(ErrorCodes.INVALID_INPUT);

      var result = await _moderationService.PreCheckTextAsync(request);
      return OkResponse(result);
    }

    // Analyze OpenAI moderation scores against thresholds.
    [HttpPost("analyze-scores")]
    public IActionResult AnalyzeScores([FromBody] OpenAiScoreRequest request)
    {
      if (request.Scores == null || request.Scores.Count == 0)
        throw new AppException(ErrorCodes.INVALID_INPUT);

      var result = _moderationService.AnalyzeOpenAiScores(request);
      return OkResponse(result);
    }

    // Get all rejection templates for moderators.
    [HttpGet("templates")]
    public IActionResult GetTemplates()
    {
      return OkResponse(_moderationService.GetRejectionTemplates());
    }

    // Get all banned and restricted content tags.
    [HttpGet("banned-tags")]
    public IActionResult GetBannedTags()
    {
      return OkResponse(_moderationService.GetBannedTags());
    }

    [HttpPost("text-ocr")] // Hoặc [HttpPost] tùy theo cách bạn muốn truyền dữ liệu
    public async Task<IActionResult> TextOCR(IFormFile file)
    {
      if (file == null || file.Length == 0)
      {
        throw new AppException(ErrorCodes.BAD_REQUEST, "Vui lòng cung cấp file ảnh.");
      }

      // Chuyển file sang byte array
      using var ms = new MemoryStream();
      await file.CopyToAsync(ms);
      var imageBytes = ms.ToArray();

      // Gọi Service OCR (Xử lý bất đồng bộ)
      var result = await _ocr.ExtractTextFromImageAsync(imageBytes);

      // Trả về kết quả (Sử dụng cấu trúc OkResponse của bạn)
      return Ok(new
      {
        Success = true,
        Data = result
      });
    }
  }
}

