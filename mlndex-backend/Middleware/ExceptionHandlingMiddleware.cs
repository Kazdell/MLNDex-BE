using Application.DTOs.Common;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace mlndex_backend.Middleware
{
  public class ExceptionHandlingMiddleware
  {
    public readonly RequestDelegate _next;
    public readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
      _next = next;
      _logger = logger;
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

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
      context.Response.ContentType = "application/json";

      ApiResponse<object?> response;
      int statusCode;

      switch (exception)
      {
        case UnauthorizedAccessException:
          statusCode = (int)HttpStatusCode.Unauthorized;
          response = new ApiResponse<object?>(false, "Unauthorized", null, ErrorCodes.UNAUTHORIZED);
          break;

        case KeyNotFoundException:
          statusCode = (int)HttpStatusCode.NotFound;
          response = new ApiResponse<object?>(false, "Resource not found", null, ErrorCodes.NOT_FOUND);
          break;

        case DbUpdateException:
          statusCode = (int)HttpStatusCode.Conflict;
          //response = new ApiResponse<object?>(false, "Database update error", null, ErrorCodes.DB_ERROR);
          response = new ApiResponse<object?>(false, $"Database update error: {exception.Message}", null, ErrorCodes.DB_ERROR);
          break;

        case ArgumentNullException:
        case ArgumentException:
          statusCode = (int)HttpStatusCode.BadRequest;
          response = new ApiResponse<object?>(false, $"Invalid argument: {exception.Message}", null, ErrorCodes.INVALID_INPUT);
          break;

        case InvalidOperationException:
          statusCode = (int)HttpStatusCode.BadRequest;
          //response = new ApiResponse<object?>(false, "Invalid operation", null, ErrorCodes.INVALID_INPUT);
          response = new ApiResponse<object?>(false, $"Invalid operation: {exception.Message}", null, ErrorCodes.INVALID_INPUT);
          break;

        default:
          statusCode = (int)HttpStatusCode.InternalServerError;
          response = new ApiResponse<object?>(false, $"An unexpected error occurred {exception.Message}. StackTrace: {exception.StackTrace}", null, ErrorCodes.INTERNAL_SERVER_ERROR);
          break;
      }

      context.Response.StatusCode = statusCode;
      await context.Response.WriteAsJsonAsync(response);
    }
  }
}
