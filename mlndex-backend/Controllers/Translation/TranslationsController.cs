using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Application.DTOs.Translation.Requests;
using Application.DTOs.Translation.Responses;
using Application.Interfaces.Data;
using Application.Interfaces.Translation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using mlndex_backend.Controllers;

namespace mlndex_backend.Controllers.Translation
{
  // Dedicated form model for multipart upload — avoids [FromForm] primitive binding failures.
  public class UploadTranslationFormRequest
  {
    public int ChapterId { get; set; }
    public int? PermissionId { get; set; } // null = unofficial
    public int? TeamId { get; set; } // Required for unofficial
    public int LanguageId { get; set; }
    public string ContentType { get; set; } = "IMAGE";
    public string? CreditsJson { get; set; }
    public string? JointTeamIdsJson { get; set; }
    // Note: 'Pages' removed from form model to prevent ASP.NET Core binding overriding matching keys and returning only 1 file.
    public string? ContentText { get; set; }
  }

  [Route("api/translations")]
  [Authorize]
  public class TranslationsController : BaseController
  {
    private readonly ITranslationService _service;
    private readonly IMlndexDbContext _db;

    public TranslationsController(ITranslationService service, IMlndexDbContext db)
    {
      _service = service;
      _db = db;
    }

    [HttpPost]
    [RequestSizeLimit(300 * 1024 * 1024)]
    public async Task<IActionResult> UploadTranslation(
        [FromForm] UploadTranslationFormRequest req,
        [FromForm] IFormFileCollection? pages)
    {
      try
      {

        // Guard: block upload if user trust score is depleted
        var currentUser = await _db.Users.FindAsync(GetUserId());
        if (currentUser?.CannotUpload == true)
          return StatusCode(403, new { message = "Tài khoản bị khoá chức năng upload do vi phạm nội quy. Vui lòng liên hệ mod để kháng cáo." });

        // Parse contentType string → enum
        if (!Enum.TryParse<Domain.Entities.ContentType>(req.ContentType, ignoreCase: true, out var contentTypeEnum))
          return BadRequest(new { message = $"Invalid contentType: {req.ContentType}" });

        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
          PropertyNameCaseInsensitive = true,
          Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        var dto = new UploadTranslationRequest
        {
          ChapterId = req.ChapterId,
          PermissionId = req.PermissionId,
          TeamId = req.TeamId,
          LanguageId = req.LanguageId,
          ContentType = contentTypeEnum,
          ContentText = req.ContentText,
          Pages = pages?.Select((file, index) => new Application.DTOs.Chapter.UploadPageDto
          {
            FileStream = file.OpenReadStream(),
            FileName = file.FileName,
            PageNumber = index + 1
          }).ToList(),
          Credits = !string.IsNullOrEmpty(req.CreditsJson)
            ? System.Text.Json.JsonSerializer.Deserialize<List<TranslationCreditItem>>(req.CreditsJson, jsonOptions)
            : null,
          JointTeamIds = !string.IsNullOrEmpty(req.JointTeamIdsJson)
            ? System.Text.Json.JsonSerializer.Deserialize<List<int>>(req.JointTeamIdsJson)
            : null
        };

        var translation = await _service.UploadTranslationAsync(dto);
        return CreatedAtAction(nameof(GetTranslationById), new { id = translation.TranslationId }, translation);
      }
      catch (Exception ex)
      {
        return BadRequestResponse(ex.Message);
      }
    }

    // Get translation details by ID.
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTranslationById(int id)
    {
      var translation = await _service.GetTranslationByIdAsync(id);
      if (translation == null) return NotFoundResponse();
      return OkResponse(translation);
    }
    
    // Get text layers for a specific page
    [HttpGet("page-layer/{pageId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPageTextLayers(int pageId, [FromServices] IPageTranslationService pageTranslationService)
    {
      try
      {
        var layers = await pageTranslationService.GetPageTextLayerAsync(pageId);
        return OkResponse(layers);
      }
      catch (Exception ex)
      {
        return BadRequestResponse(ex.Message);
      }
    }

    // Generate text layers for a specific page using AI OCR and Translation
    [HttpPost("page-layer/generate/{pageId}")]
    public async Task<IActionResult> GeneratePageTextLayers(int pageId, [FromServices] IPageTranslationService pageTranslationService, [FromQuery] string targetLanguage = "Vietnamese")
    {
      try
      {
        var layers = await pageTranslationService.GeneratePageTextLayerAsync(pageId, targetLanguage);
        return OkResponse(layers);
      }
      catch (Exception ex)
      {
        return BadRequestResponse(ex.Message);
      }
    }

    // Get all translations by series ID.
    [HttpGet("series/{seriesId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTranslationsBySeries(int seriesId)
    {
      var translations = await _service.GetTranslationsBySeriesAsync(seriesId);
      return OkResponse(translations);
    }

    // Get all translations (Admin/Mod only in future).
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllTranslations()
    {
      var translations = await _service.GetAllTranslationsAsync();
      return OkResponse(translations);
    }

    // Edit translation metadata or content.
    [HttpPut("{id}")]
    public async Task<IActionResult> EditTranslation(int id, [FromBody] EditTranslationRequest dto)
    {
      try
      {
        var currentUser = await _db.Users.FindAsync(GetUserId());
        if (currentUser?.CannotUpload == true)
          return StatusCode(403, new { message = "Tài khoản bị khoá chức năng upload do vi phạm nội quy. Vui lòng liên hệ mod để kháng cáo." });

        var translation = await _service.EditTranslationAsync(id, dto);
        return OkResponse(translation);
      }
      catch (Exception ex)
      {
        return BadRequestResponse(ex.Message);
      }
    }

    // Delete a translation.
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTranslation(int id)
    {
      try
      {
        var success = await _service.DeleteTranslationAsync(id);
        if (!success) return NotFoundResponse("Translation not found or unauthorized.");
        return NoContent();
      }
      catch (Exception ex)
      {
        return BadRequestResponse(ex.Message);
      }
    }

    [HttpGet("{id}/moderation-status")]
    public async Task<IActionResult> GetTranslationModerationStatus(int id)
    {
      var translation = await _db.Translations.FindAsync(id);
      if (translation == null)
        return NotFoundResponse("Bản dịch không tồn tại.");

      if (translation.ModerationStatus != Domain.Entities.ModerationStatus.PENDING)
      {
        return OkResponse(new
        {
          status = translation.ModerationStatus == Domain.Entities.ModerationStatus.REJECTED ? "failed" : "completed",
          result = translation.AiScoresJson != null
            ? System.Text.Json.JsonSerializer.Deserialize<object>(translation.AiScoresJson)
            : new { isClean = true, flaggedCategories = new List<string>(), flags = new { } }
        });
      }

      // If pending, get queue position
      var currentQueueItem = await _db.ModerationQueues
        .Where(q => q.ContentType == Domain.Entities.ModerationQueueContentType.TRANSLATION && q.ContentId == id)
        .FirstOrDefaultAsync();

      int pos = 0;
      string statusStr = "pending";

      if (currentQueueItem != null)
      {
        if (currentQueueItem.Status == Domain.Entities.QueueStatus.IN_REVIEW)
        {
           statusStr = "processing";
        }
        else
        {
           pos = await _db.ModerationQueues
            .Where(q => q.ContentType == Domain.Entities.ModerationQueueContentType.TRANSLATION && q.Status == Domain.Entities.QueueStatus.PENDING && q.FlaggedAt < currentQueueItem.FlaggedAt)
            .CountAsync() + 1;
        }
      }

      return OkResponse(new
      {
        status = statusStr,
        queuePos = pos
      });
    }

    [HttpPost("{id}/debug-moderate")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> DebugModerate(int id, [FromServices] Application.Interfaces.AIModeration.IModerationService moderationService)
    {
      try
      {
        var result = await moderationService.RunTranslationModerationAsync(id);
        return OkResponse(new { message = "Moderation completed successfully.", result = result });
      }
      catch (Exception ex)
      {
         return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message, stackTrace = ex.StackTrace, innerException = ex.InnerException?.Message });
      }
    }

    [HttpPost("{id}/debug-enqueue")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> DebugEnqueue(int id, [FromServices] Application.Interfaces.AIModeration.IModerationService moderationService)
    {
      try
      {
        await moderationService.EnqueueTranslationForModerationAsync(id);
        return OkResponse(new { message = "Translation enqueued for moderation successfully." });
      }
      catch (Exception ex)
      {
         return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message, stackTrace = ex.StackTrace, innerException = ex.InnerException?.Message });
      }
    }
  }
}
