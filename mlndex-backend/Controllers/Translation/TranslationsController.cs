using System;
using System.Threading.Tasks;
using Application.DTOs.Translation;
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

        public TranslationsController(ITranslationService service)
        {
            _service = service;
        }

        // Upload a new translation (Image or Text).
        [HttpPost]
        public async Task<IActionResult> UploadTranslation([FromBody] UploadTranslationDto dto)
        {
            try
            {
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
