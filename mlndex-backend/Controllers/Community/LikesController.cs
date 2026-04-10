using Application.DTOs.Common;
using Application.Exceptions;
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
        throw new AppException(ErrorCodes.INVALID_INPUT);

      var userId = GetUserId();

      if (userId == 0)
        throw new AppException(ErrorCodes.UNAUTHORIZED);

      var result = await _likeService.ToggleAsync(userId, request, cancellationToken);
      return OkResponse(result, "Toggled");
    }
  }
}
