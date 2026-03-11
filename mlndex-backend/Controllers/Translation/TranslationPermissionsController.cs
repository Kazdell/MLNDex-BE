using System;
using System.Threading.Tasks;
using Application.DTOs.Translation;
using Application.Interfaces.Translation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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

        // Request permission from series creator to translate a series.
        [Authorize]
        [HttpPost("request")]
        public async Task<IActionResult> RequestPermission([FromBody] RequestPermissionDto dto)
        {
            try
            {
                var permission = await _service.RequestPermissionAsync(dto);
                return OkResponse(permission);
            }
            catch (Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        // Series creator reviews (Approve/Reject) a translation request.
        [Authorize]
        [HttpPut("{id}/status")]
        public async Task<IActionResult> ReviewPermission(int id, [FromBody] ReviewPermissionDto dto)
        {
            try
            {
                var permission = await _service.ReviewPermissionAsync(id, dto);
                return OkResponse(permission);
            }
            catch (Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }
    }
}
