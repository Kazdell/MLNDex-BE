using System;
using System.Threading.Tasks;
using Application.DTOs.Translation;
using Application.Interfaces.Translation;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Translation
{
    [Route("api/translations")]
    [ApiController]
    public class TranslationsController : ControllerBase
    {
        private readonly ITranslationService _service;

        public TranslationsController(ITranslationService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> UploadTranslation([FromBody] UploadTranslationDto dto)
        {
            try
            {
                int uploaderId = 1; // MOCK ONLY
                var translation = await _service.UploadTranslationAsync(uploaderId, dto);
                return CreatedAtAction(nameof(GetTranslationById), new { id = translation.TranslationId }, translation);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTranslationById(int id)
        {
            var translation = await _service.GetTranslationByIdAsync(id);
            if (translation == null) return NotFound();
            return Ok(translation);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllTranslations()
        {
            var translations = await _service.GetAllTranslationsAsync();
            return Ok(translations);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> EditTranslation(int id, [FromBody] EditTranslationDto dto)
        {
            try
            {
                int uploaderId = 1; // MOCK ONLY
                var translation = await _service.EditTranslationAsync(id, uploaderId, dto);
                return Ok(translation);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTranslation(int id)
        {
            try
            {
                int uploaderId = 1; // MOCK ONLY
                var success = await _service.DeleteTranslationAsync(id, uploaderId);
                if (!success) return NotFound(new { message = "Translation not found or unauthorized." });
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
