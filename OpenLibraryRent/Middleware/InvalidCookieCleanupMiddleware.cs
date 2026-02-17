using OpenLibraryRent.Constants;

namespace OpenLibraryRent.Middleware;

/// <summary>
/// 無効なCookieをクリーンアップするミドルウェア
/// </summary>
public class InvalidCookieCleanupMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InvalidCookieCleanupMiddleware> _logger;

    public InvalidCookieCleanupMiddleware(RequestDelegate next, ILogger<InvalidCookieCleanupMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);
    }
}

public static class InvalidCookieCleanupMiddlewareExtensions
{
    public static IApplicationBuilder UseInvalidCookieCleanup(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<InvalidCookieCleanupMiddleware>();
    }
}
