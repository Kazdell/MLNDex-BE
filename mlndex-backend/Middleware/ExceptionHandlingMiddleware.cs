using Application.DTOs.Common;
using Microsoft.EntityFrameworkCore;
using System.Net;

using Application.Resources;
using Microsoft.Extensions.Localization;

namespace mlndex_backend.Middleware
{
  public class ExceptionHandlingMiddleware
  {
    public readonly RequestDelegate _next;
    public readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IStringLocalizer<SharedResource> localizer)
    {
      _next = next;
      _logger = logger;
      _localizer = localizer;
    }

    public async Task InvokeAsync(HttpContext context)
    {
      try
      {
        await _next(context);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unhandled exception caught by middleware");
        await HandleExceptionAsync(context, ex);
      }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
      context.Response.ContentType = "application/json";

      ApiResponse<object?> response;
      int statusCode;

      switch (exception)
      {
        case Application.Exceptions.AppException appEx:
          statusCode = appEx.StatusCode;
          // Use CustomMessage if provided, else rely on Localizer using ErrorCode
          bool hasCustomMessage = appEx.Message != appEx.ErrorCode;
          string localizedMessage = hasCustomMessage ? appEx.Message : _localizer[appEx.ErrorCode]?.Value ?? appEx.Message;
          response = new ApiResponse<object?>(false, localizedMessage, null, appEx.ErrorCode);
          break;

        case UnauthorizedAccessException:
          statusCode = (int)HttpStatusCode.Unauthorized;
          response = new ApiResponse<object?>(false, _localizer[ErrorCodes.UNAUTHORIZED]?.Value ?? "Unauthorized", null, ErrorCodes.UNAUTHORIZED);
          break;

        case KeyNotFoundException:
          statusCode = (int)HttpStatusCode.NotFound;
          response = new ApiResponse<object?>(false, _localizer[ErrorCodes.NOT_FOUND]?.Value ?? "Resource not found", null, ErrorCodes.NOT_FOUND);
          break;

        case DbUpdateException:
          statusCode = (int)HttpStatusCode.Conflict;
          // response = new ApiResponse<object?>(false, "Database update error", null, ErrorCodes.DB_ERROR);
          response = new ApiResponse<object?>(false, $"Database update error: {exception.Message}", null, ErrorCodes.DB_ERROR);
          break;



        default:
          statusCode = (int)HttpStatusCode.InternalServerError;
          response = new ApiResponse<object?>(false, _localizer[ErrorCodes.INTERNAL_SERVER_ERROR]?.Value ?? "An unexpected error occurred", null, ErrorCodes.INTERNAL_SERVER_ERROR);
          break;
      }

      context.Response.StatusCode = statusCode;
      await context.Response.WriteAsJsonAsync(response);
    }
  }
}
