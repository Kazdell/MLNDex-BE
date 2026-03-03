using System;
using System.Threading.Tasks;
using Application.DTOs.Translation;
using Application.Interfaces.Translation;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Translation
{
    [Route("api/translation-permissions")]
    [ApiController]
    public class TranslationPermissionsController : ControllerBase
    {
        private readonly ITranslationPermissionService _service;

        public TranslationPermissionsController(ITranslationPermissionService service)
        {
            _service = service;
        }

        [HttpPost("request")]
        public async Task<IActionResult> RequestPermission([FromBody] RequestPermissionDto dto)
        {
            try
            {
                int requesterId = 1; // MOCK ONLY
                var permission = await _service.RequestPermissionAsync(requesterId, dto);
                return Ok(permission);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> ReviewPermission(int id, [FromBody] ReviewPermissionDto dto)
        {
            try
            {
                int creatorId = 1; // MOCK ONLY
                var permission = await _service.ReviewPermissionAsync(id, creatorId, dto);
                return Ok(permission);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
