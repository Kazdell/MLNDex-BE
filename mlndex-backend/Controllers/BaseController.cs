using Application.DTOs.Common;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers
{
  [ApiController]
  public abstract class BaseController : ControllerBase
  {
    protected IActionResult OkResponse<T>(T? data, string message = "Success")
    {
      return Ok(new ApiResponse<T>(true, message, data));
    }

    protected IActionResult BadRequestResponse(string message, string errorCode = ErrorCodes.BAD_REQUEST)
    {
      return BadRequest(new ApiResponse<object?>(false, message, null, errorCode));
    }

    protected IActionResult UnauthorizedResponse(string message = "Unauthorized", string errorCode = ErrorCodes.UNAUTHORIZED)
    {
      return Unauthorized(new ApiResponse<object?>(false, message, null, errorCode));
    }

    protected IActionResult NotFoundResponse(string message = "Not found", string errorCode = ErrorCodes.NOT_FOUND)
    {
      return NotFound(new ApiResponse<object?>(false, message, null, errorCode));
    }

    protected IActionResult ConflictResponse(string message = "Conflict", string errorCode = ErrorCodes.CONFLICT)
    {
      return Conflict(new ApiResponse<object?>(false, message, null, errorCode));
    }

    protected IActionResult ErrorResponse(string message, string errorCode = ErrorCodes.INTERNAL_SERVER_ERROR, int statusCode = 500)
    {
      var response = new ApiResponse<object?>(false, message, null, errorCode);
      return StatusCode(statusCode, response);
    }

    protected int GetUserId()
    {
      var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("UserId");
      return int.TryParse(userIdClaim?.Value, out var id) ? id : 0;
    }
  }
}
