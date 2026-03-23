using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Application.DTOs.Translation;
using Application.Interfaces.Data;
using Application.Interfaces.Translation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using mlndex_backend.Controllers;

namespace mlndex_backend.Controllers.Translation
{
  // Dedicated form model for multipart upload — avoids [FromForm] primitive binding failures.
  public class UploadTranslationFormRequest
  {
    public int ChapterId { get; set; }
    public int PermissionId { get; set; }
    public int LanguageId { get; set; }
    public string ContentType { get; set; } = "IMAGE";
    public string? CreditsJson { get; set; }
    public string? JointTeamIdsJson { get; set; }
    public List<IFormFile>? Pages { get; set; }
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
    public async Task<IActionResult> UploadTranslation([FromForm] UploadTranslationFormRequest req)
    {
      try
      {
        Console.WriteLine($"[DEBUG-UPLOAD] chapterId={req.ChapterId}, permissionId={req.PermissionId}, languageId={req.LanguageId}, contentType={req.ContentType}");

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

        var dto = new UploadTranslationDto
        {
          ChapterId = req.ChapterId,
          PermissionId = req.PermissionId,
          LanguageId = req.LanguageId,
          ContentType = contentTypeEnum,
          ContentText = req.ContentText,
          Pages = req.Pages?.Select((file, index) => new Application.DTOs.Chapter.UploadPageDto
          {
            FileStream = file.OpenReadStream(),
            FileName = file.FileName,
            PageNumber = index + 1
          }).ToList(),
          Credits = !string.IsNullOrEmpty(req.CreditsJson)
            ? System.Text.Json.JsonSerializer.Deserialize<List<TranslationCreditDto>>(req.CreditsJson, jsonOptions)
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
    public async Task<IActionResult> EditTranslation(int id, [FromBody] EditTranslationDto dto)
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
  }
}
