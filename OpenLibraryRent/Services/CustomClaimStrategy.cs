using Finbuckle.MultiTenant.Abstractions;
using System.Security.Claims;
using OpenLibraryRent.Constants;

namespace OpenLibraryRent.Services;

/// <summary>
/// カスタム Claim Strategy
/// OIDC コールバックパスでは tenant 解決をスキップ
/// </summary>
public class CustomClaimStrategy : IMultiTenantStrategy
{
    private readonly string _claimType;
    private readonly ILogger<CustomClaimStrategy> _logger;

    public CustomClaimStrategy(string claimType, ILogger<CustomClaimStrategy> logger)
    {
        _claimType = claimType;
        _logger = logger;
    }

    public async Task<string?> GetIdentifierAsync(object context)
    {
        if (context is not HttpContext httpContext)
        {
            return null;
        }

        // 静的ファイルは tenant 解決をスキップ
        if (AuthenticationConstants.StaticFilePaths.IsStaticFile(httpContext.Request.Path.Value ?? string.Empty))
        {
            return null;
        }

        // OIDC コールバックパスでは query から tenant を解決
        if (httpContext.Request.Path.StartsWithSegments(AuthenticationConstants.Paths.OidcCallbackPath))
        {
            var tenantFromQuery = httpContext.Request.Query[AuthenticationConstants.TenantClaimType].ToString();
            if (!string.IsNullOrWhiteSpace(tenantFromQuery))
            {
                _logger.LogInformation("[CustomClaimStrategy] Tenant resolved from query on OIDC callback: {TenantId}", tenantFromQuery);
                return tenantFromQuery;
            }

            _logger.LogInformation("[CustomClaimStrategy] OIDC callback without tenant query. Skipping tenant resolution.");
            return null;
        }

        // 1. Claim から tenant を取得
        var tenantId = httpContext.User?.FindFirst(_claimType)?.Value;
        if (!string.IsNullOrEmpty(tenantId))
        {
            _logger.LogInformation("[CustomClaimStrategy] Tenant resolved from claim: {TenantId}", tenantId);
            return tenantId;
        }

        // 2. Route parameter から tenant を取得
        try
        {
            var tenantFromRoute = httpContext.GetRouteValue(_claimType)?.ToString();
            if (!string.IsNullOrEmpty(tenantFromRoute))
            {
                _logger.LogInformation("[CustomClaimStrategy] Tenant resolved from route: {TenantId}", tenantFromRoute);
                return tenantFromRoute;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CustomClaimStrategy] Error resolving tenant from route");
        }

        // 3. URL パスから直接 tenant を抽出
        try
        {
            var pathValue = httpContext.Request?.Path.Value;
            if (!string.IsNullOrEmpty(pathValue))
            {
                var segments = pathValue.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 0)
                {
                    var firstSegment = segments[0];
                    if (!string.IsNullOrWhiteSpace(firstSegment) &&
                        !string.Equals(firstSegment, "_app", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.Equals(firstSegment, "api", StringComparison.OrdinalIgnoreCase))
                        {
                            return null;
                        }

                        if (firstSegment.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                        {
                            _logger.LogInformation("[CustomClaimStrategy] Tenant resolved from path: {TenantId}", firstSegment);
                            return firstSegment;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CustomClaimStrategy] Error resolving tenant from path");
        }

        return null;
    }
}
