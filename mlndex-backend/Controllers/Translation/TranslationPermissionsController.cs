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
        private readonly Application.Interfaces.Data.IMlndexDbContext _db;

        public TranslationPermissionsController(ITranslationPermissionService service, Application.Interfaces.Data.IMlndexDbContext db)
        {
            _service = service;
            _db = db;
        }

        // Request permission from series creator to translate a series.
        [Authorize]
        [HttpPost("request")]
        public async Task<IActionResult> RequestPermission([FromBody] RequestPermissionDto dto)
        {
            try
            {
                var currentUser = await _db.Users.FindAsync(GetUserId());
                if (currentUser?.CannotUpload == true)
                    return StatusCode(403, new { message = "Tài khoản bị khoá chức năng upload do vi phạm nội quy. Vui lòng liên hệ mod để kháng cáo." });

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

        [Authorize]
        [HttpGet("team/{teamId}")]
        public async Task<IActionResult> GetTeamPermissions(int teamId)
        {
            try
            {
                var permissions = await _service.GetTeamPermissionsAsync(teamId);
                return OkResponse(permissions);
            }
            catch (Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        [Authorize]
        [HttpGet("creator")]
        public async Task<IActionResult> GetCreatorPermissions()
        {
            try
            {
                var userId = GetUserId(); 
                if (userId == 0) return UnauthorizedResponse();

                var permissions = await _service.GetCreatorPermissionsAsync(userId);
                return OkResponse(permissions);
            }
            catch (Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }
    }
}
