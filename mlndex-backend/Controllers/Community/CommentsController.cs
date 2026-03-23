using System.Security.Claims;
using Application.DTOs.Community;
using Application.Interfaces.Community;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Community
{
  [Authorize]
  [Route("api/comments")]
  public class CommentsController : BaseController
  {
    private readonly ICommentService _service;

    public CommentsController(ICommentService service)
    {
      _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCommentRequest request,
        CancellationToken cancellationToken
    )
    {
      if (!ModelState.IsValid)
        return BadRequestResponse("Invalid payload");

      var userId = GetUserId();

      if (userId == 0)
        return UnauthorizedResponse("Invalid user context");

      try
      {
        var result = await _service.CreateAsync(userId, request, cancellationToken);
        return OkResponse(result, "Created");
      }
      catch (KeyNotFoundException ex)
      {
        return NotFoundResponse(ex.Message);
      }
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int targetId,
        [FromQuery] CommentTargetType targetType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default
    )
    {
      var result = await _service.GetByTargetAsync(
          targetId,
          targetType,
          page,
          pageSize,
          cancellationToken
      );
      return OkResponse(result);
    }

    [HttpDelete("{commentId:int}")]
    public async Task<IActionResult> Delete(int commentId, CancellationToken cancellationToken)
    {
      var userId = GetUserId();

      if (userId == 0)
        return UnauthorizedResponse("Invalid user context");

      try
      {
        await _service.DeleteAsync(commentId, userId, cancellationToken);
        return OkResponse<object?>(null, "Deleted");
      }
      catch (KeyNotFoundException ex)
      {
        return NotFoundResponse(ex.Message);
      }
      catch (UnauthorizedAccessException ex)
      {
        return UnauthorizedResponse(ex.Message);
      }
    }
  }
}
