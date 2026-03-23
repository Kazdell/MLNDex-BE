using mlndex_backend.Middleware;

namespace mlndex_backend.Extension
{
  public static class ExceptionMiddlewareExtensions
  {
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
      return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
  }
}
