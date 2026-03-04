using System;
using System.Threading.Tasks;
using Application.DTOs.Translation;
using Application.Interfaces.Translation;
using Microsoft.AspNetCore.Mvc;
using mlndex_backend.Controllers;

namespace mlndex_backend.Controllers.Translation
{
    [Route("api/translation-permissions")]
    public class TranslationPermissionsController : BaseController
    {
        private readonly ITranslationPermissionService _service;

        public TranslationPermissionsController(ITranslationPermissionService service)
        {
            _service = service;
        }

        // Request permission from series creator to translate a chapter.
        [HttpPost("request")]
        public async Task<IActionResult> RequestPermission([FromBody] RequestPermissionDto dto)
        {
            try
            {
                int requesterId = 1; // TODO: Get from Auth claims
                var permission = await _service.RequestPermissionAsync(requesterId, dto);
                return OkResponse(permission);
            }
            catch (Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        // Series creator reviews (Approve/Reject) a translation request.
        [HttpPut("{id}/status")]
        public async Task<IActionResult> ReviewPermission(int id, [FromBody] ReviewPermissionDto dto)
        {
            try
            {
                int creatorId = 1; // TODO: Get from Auth claims
                var permission = await _service.ReviewPermissionAsync(id, creatorId, dto);
                return OkResponse(permission);
            }
            catch (Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }
    }
}
