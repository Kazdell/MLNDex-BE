using System;
using System.Threading.Tasks;
using Application.DTOs.Translation;
using Application.Interfaces.Data;
using Application.Interfaces.Translation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using mlndex_backend.Controllers;

namespace mlndex_backend.Controllers.Translation
{
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
            [FromForm] int chapterId,
            [FromForm] int permissionId,
            [FromForm] int languageId,
            [FromForm] Domain.Entities.ContentType contentType,
            [FromForm] string? creditsJson, // JSON string of List<TranslationCreditDto>
            [FromForm] string? jointTeamIdsJson, // JSON string of List<int>
            [FromForm] Microsoft.AspNetCore.Http.IFormFileCollection? pages,
            [FromForm] string? contentText)
        {
            try
            {
                // Guard: Block upload if user trust score is depleted
                var currentUser = await _db.Users.FindAsync(GetUserId());
                if (currentUser?.CannotUpload == true)
                    return StatusCode(403, new { message = "Tài khoản bị khoá chức năng upload do vi phạm nội quy. Vui lòng liên hệ mod để kháng cáo." });

                var dto = new UploadTranslationDto
                {
                    ChapterId = chapterId,
                    PermissionId = permissionId,
                    LanguageId = languageId,
                    ContentType = contentType,
                    ContentText = contentText,
                    Pages = pages?.Select((file, index) => new Application.DTOs.Chapter.UploadPageDto
                    {
                        FileStream = file.OpenReadStream(),
                        FileName = file.FileName,
                        PageNumber = index + 1
                    }).ToList(),
                    Credits = !string.IsNullOrEmpty(creditsJson) ? System.Text.Json.JsonSerializer.Deserialize<List<TranslationCreditDto>>(creditsJson) : null,
                    JointTeamIds = !string.IsNullOrEmpty(jointTeamIdsJson) ? System.Text.Json.JsonSerializer.Deserialize<List<int>>(jointTeamIdsJson) : null
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
