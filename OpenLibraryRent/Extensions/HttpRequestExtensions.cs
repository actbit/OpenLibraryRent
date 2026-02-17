using Microsoft.AspNetCore.Http;
using OpenLibraryRent.Constants;

namespace OpenLibraryRent.Extensions;

/// <summary>
/// HTTP リクエストに関する拡張メソッド
/// </summary>
public static class HttpRequestExtensions
{
    /// <summary>
    /// リクエストが API リクエストかどうかを判定
    /// </summary>
    public static bool IsApiRequest(this HttpRequest request)
    {
        var path = request.Path.Value ?? string.Empty;
        if (path.EndsWith("/auth/me", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/auth/check", StringComparison.OrdinalIgnoreCase))
            return true;

        if (AuthenticationConstants.Paths.IsApiPath(path))
            return true;

        var accept = request.Headers.Accept.ToString();
        if (!string.IsNullOrEmpty(accept) && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            return true;

        var xRequestedWith = request.Headers.XRequestedWith.ToString();
        if (string.Equals(xRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
