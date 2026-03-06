using Application.DTOs.Moderation;
using Application.Interfaces.Moderation;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Admin
{
    [Route("api/admin/moderators")]
    public class ModeratorsController : BaseController
    {
        private readonly IModeratorAdminService _service;

        public ModeratorsController(IModeratorAdminService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetList(
            [FromQuery] ModeratorListRequest request,
            CancellationToken cancellationToken
        )
        {
            var result = await _service.GetModeratorsAsync(request, cancellationToken);
            return OkResponse(result);
        }

        [HttpPost]
        public async Task<IActionResult> Assign(
            [FromBody] AssignModeratorRequest request,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid)
                return BadRequestResponse("Invalid payload");

            try
            {
                var result = await _service.AssignAsync(request.UserId, cancellationToken);
                return OkResponse(result, "Moderator assigned");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse(ex.Message);
            }
        }

        [HttpDelete("{userId:int}")]
        public async Task<IActionResult> Remove(int userId, CancellationToken cancellationToken)
        {
            try
            {
                await _service.RemoveAsync(userId, cancellationToken);
                return OkResponse<object>(null, "Moderator removed");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse(ex.Message);
            }
        }
    }
}
