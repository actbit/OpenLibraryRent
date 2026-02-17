using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using OpenLibraryRent.Constants;

namespace OpenLibraryRent.Authentication;

/// <summary>
/// テナント固有のCookie名を使用するカスタムCookieマネージャー
/// </summary>
public class TenantAwareCookieManager : ICookieManager
{
    private readonly ChunkingCookieManager _innerManager = new ChunkingCookieManager();
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TenantAwareCookieManager> _logger;

    public TenantAwareCookieManager(IHttpContextAccessor httpContextAccessor, ILogger<TenantAwareCookieManager> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private string GetCookieName(string baseCookieName)
    {
        try
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return baseCookieName;
            }

            if (context.Request?.Path.StartsWithSegments(AuthenticationConstants.StaticFilePaths.SvelteKitAssets) == true)
            {
                return baseCookieName;
            }

            string? tenant = null;
            if (context.Request?.Path.StartsWithSegments(AuthenticationConstants.Paths.OidcCallbackPath) == true)
            {
                tenant = context.Request?.Query[AuthenticationConstants.TenantClaimType].ToString();
            }
            else
            {
                if (context.Items?.TryGetValue(AuthenticationConstants.Cookie.TenantForCookieKey, out var tenantObj) == true)
                {
                    tenant = tenantObj?.ToString();
                }

                if (string.IsNullOrEmpty(tenant))
                {
                    tenant = context.GetRouteValue(AuthenticationConstants.TenantClaimType)?.ToString();
                }

                if (string.IsNullOrEmpty(tenant))
                {
                    var pathValue = context.Request?.Path.Value;
                    if (!string.IsNullOrEmpty(pathValue))
                    {
                        var segments = pathValue.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                        if (segments.Length > 0)
                        {
                            var firstSegment = segments[0];
                            if (!string.IsNullOrWhiteSpace(firstSegment) &&
                                firstSegment.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                            {
                                tenant = firstSegment;
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(tenant))
            {
                return baseCookieName;
            }

            var tenantCookieName = $"{baseCookieName}{AuthenticationConstants.Cookie.TenantCookieNameSeparator}{tenant}{AuthenticationConstants.Cookie.TenantCookieSuffix}";
            return tenantCookieName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetCookieName, using base cookie name");
            return baseCookieName;
        }
    }

    public string? GetRequestCookie(HttpContext context, string key)
    {
        try
        {
            if (context == null)
            {
                return null;
            }

            var tenantAwareName = GetCookieName(key);
            var tenantCookie = _innerManager.GetRequestCookie(context, tenantAwareName);
            if (!string.IsNullOrEmpty(tenantCookie))
            {
                return tenantCookie;
            }

            return _innerManager.GetRequestCookie(context, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetRequestCookie");
            return null;
        }
    }

    public void AppendResponseCookie(HttpContext context, string key, string? value, CookieOptions options)
    {
        try
        {
            if (context == null || options == null)
            {
                return;
            }

            var tenantAwareName = GetCookieName(key);
            _innerManager.AppendResponseCookie(context, tenantAwareName, value, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AppendResponseCookie");
        }
    }

    public void DeleteCookie(HttpContext context, string key, CookieOptions options)
    {
        try
        {
            if (context == null || options == null)
            {
                return;
            }

            var tenantAwareName = GetCookieName(key);
            _innerManager.DeleteCookie(context, tenantAwareName, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteCookie");
        }
    }
}
