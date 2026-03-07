using System.Security.Claims;
using Application.DTOs.Community;
using Application.Interfaces.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Community
{
    [Authorize]
    [Route("api/likes")]
    public class LikesController : BaseController
    {
        private readonly ILikeService _likeService;

        public LikesController(ILikeService likeService)
        {
            _likeService = likeService;
        }

        [HttpPost("toggle")]
        public async Task<IActionResult> Toggle(
            [FromBody] LikeRequest request,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid)
                return BadRequestResponse("Invalid payload");

            var userIdValue =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;

            if (!int.TryParse(userIdValue, out var userId))
                return UnauthorizedResponse("Invalid user context");

            var result = await _likeService.ToggleAsync(userId, request, cancellationToken);
            return OkResponse(result, "Toggled");
        }
    }
}
