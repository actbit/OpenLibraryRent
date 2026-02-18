using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;
using OpenLibraryRent.Models;
using OpenLibraryRent.Services;
using OpenLibraryRent.Constants;

namespace OpenLibraryRent.Extensions;

public static class OpenIdConnectExtensions
{
    public static AuthenticationBuilder AddOpenIdConnectConfiguration(
        this AuthenticationBuilder builder,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return builder.AddOpenIdConnect("oidc", options =>
        {
            options.SignInScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
            options.RequireHttpsMetadata = !environment.IsDevelopment();

            options.CorrelationCookie.SameSite = SameSiteMode.None;
            options.CorrelationCookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.NonceCookie.SameSite = SameSiteMode.None;
            options.NonceCookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;

            options.CallbackPath = "/auth/signin-oidc";

            options.Configuration = new OpenIdConnectConfiguration();

            options.Authority = string.Empty;
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidateAudience = true;

            options.ClientId = "placeholder-client-id";
            options.ClientSecret = string.Empty;

            options.ResponseType = "code";
            options.ResponseMode = "query";
            options.SaveTokens = false;

            options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;

            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");

            options.Events = new OpenIdConnectEvents
            {
                OnRedirectToIdentityProvider = async ctx => await OnRedirectToIdentityProvider(ctx),
                OnAuthorizationCodeReceived = async ctx => await OnAuthorizationCodeReceived(ctx),
                OnTokenValidated = async ctx => await OnTokenValidated(ctx),
                OnAuthenticationFailed = ctx =>
                {
                    var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ctx.Exception, "[OIDC] Authentication failed");
                    return Task.CompletedTask;
                },
                OnRemoteFailure = ctx =>
                {
                    var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ctx.Failure, "[OIDC] Remote failure: {Error}", ctx.Failure?.Message);
                    return Task.CompletedTask;
                }
            };
        });
    }

    private static async Task OnRedirectToIdentityProvider(RedirectContext ctx)
    {
        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

        var requestPath = ctx.HttpContext.Request.Path.Value ?? string.Empty;
        var tenantFromRoute = ctx.HttpContext.GetRouteValue("tenant")?.ToString();
        var expectedLoginPath = string.IsNullOrEmpty(tenantFromRoute)
            ? AuthenticationConstants.Paths.LoginPath
            : $"/{tenantFromRoute}{AuthenticationConstants.Paths.LoginPath}";

        if (!requestPath.Equals(expectedLoginPath, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("[OnRedirectToIdentityProvider] Blocked OIDC redirect from non-login path: {Path}", requestPath);
            ctx.HandleResponse();
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var tenantId = ctx.HttpContext.GetRouteValue("tenant")?.ToString();

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            logger.LogWarning("Tenant not found on redirect request");
            ctx.HandleResponse();
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsync("Tenant is required.");
            return;
        }

        var configuration = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var dbContext = ctx.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var tenantInfo = await dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == tenantId);

        var publicBaseUrl = configuration["Authentication:PublicBaseUrl"];
        if (!string.IsNullOrWhiteSpace(publicBaseUrl) &&
            Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var publicBaseUri))
        {
            if (tenantInfo?.Detail?.HasOidcSettings() ?? false)
            {
                ctx.ProtocolMessage.RedirectUri =
                    $"{publicBaseUri.Scheme}://{publicBaseUri.Authority}{AuthenticationConstants.Paths.OidcCallbackPath}?tenant={Uri.EscapeDataString(tenantId)}";
            }
            else
            {
                ctx.ProtocolMessage.RedirectUri =
                    $"{publicBaseUri.Scheme}://{publicBaseUri.Authority}{AuthenticationConstants.Paths.OidcCallbackPath}";
            }
        }
        else
        {
            var scheme = ctx.HttpContext.Request.Scheme;
            var host = ctx.HttpContext.Request.Host;
            if (tenantInfo?.Detail?.HasOidcSettings() ?? false)
            {
                ctx.ProtocolMessage.RedirectUri =
                    $"{scheme}://{host}{AuthenticationConstants.Paths.OidcCallbackPath}?tenant={Uri.EscapeDataString(tenantId)}";
            }
            else
            {
                ctx.ProtocolMessage.RedirectUri =
                    $"{scheme}://{host}{AuthenticationConstants.Paths.OidcCallbackPath}";
            }
        }

        ctx.Properties.Items["tenant"] = tenantId;

        if (!string.IsNullOrEmpty(ctx.Options.ClientId))
        {
            ctx.ProtocolMessage.ClientId = ctx.Options.ClientId;
        }

        if (ctx.Options.Configuration?.AuthorizationEndpoint != null)
        {
            ctx.ProtocolMessage.IssuerAddress = ctx.Options.Configuration.AuthorizationEndpoint;
        }

        if (string.IsNullOrEmpty(ctx.Options.ClientId) && ctx.Options.Configuration?.AuthorizationEndpoint == null)
        {
            logger.LogError("[OnRedirectToIdentityProvider] OIDC configuration not found for tenant: {TenantId}", tenantId);
            ctx.HandleResponse();
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new { error = "OIDC configuration is not properly configured for this tenant." });
            return;
        }
    }

    private static async Task OnAuthorizationCodeReceived(AuthorizationCodeReceivedContext ctx)
    {
        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

        var tenantId = ResolveTenantId(ctx.Properties, ctx.HttpContext);

        if (string.IsNullOrEmpty(tenantId))
        {
            logger.LogError("[OnAuthorizationCodeReceived] Tenant not found");
            ctx.Fail("Tenant not found");
            return;
        }

        if (!string.IsNullOrEmpty(ctx.Options.ClientId))
        {
            ctx.TokenEndpointRequest.ClientId = ctx.Options.ClientId;
        }

        if (!string.IsNullOrEmpty(ctx.Options.ClientSecret))
        {
            ctx.TokenEndpointRequest.ClientSecret = ctx.Options.ClientSecret;
        }

        logger.LogInformation("[OnAuthorizationCodeReceived] TokenEndpoint configured for tenant: {TenantId}", tenantId);
    }

    private static async Task OnTokenValidated(TokenValidatedContext ctx)
    {
        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

        var tenantId = ResolveTenantId(ctx.Properties, ctx.HttpContext);

        if (string.IsNullOrEmpty(tenantId))
        {
            logger.LogWarning("Tenant not found in query parameter");
            ctx.Fail("Tenant not found");
            return;
        }

        var audience = ctx.Principal?.FindFirst("aud")?.Value;
        var expectedAudience = ctx.Options.ClientId;

        if (!string.IsNullOrEmpty(expectedAudience))
        {
            if (string.IsNullOrEmpty(audience))
            {
                logger.LogError("[OnTokenValidated] Audience claim not found in token for tenant: {TenantId}", tenantId);
                ctx.Fail("Audience claim missing");
                return;
            }

            if (audience != expectedAudience)
            {
                logger.LogError("[OnTokenValidated] Audience validation failed for tenant: {TenantId}", tenantId);
                ctx.Fail("Invalid audience");
                return;
            }
        }

        var userSync = ctx.HttpContext.RequestServices.GetRequiredService<UserSyncService>();
        var syncResult = await userSync.SyncUserAsync(ctx.Principal);

        if (!syncResult)
        {
            logger.LogWarning("[OnTokenValidated] User sync failed (email restriction?) for tenant: {TenantId}", tenantId);
            ctx.Fail("Access denied. Your email address is not allowed.");
            return;
        }

        var identity = (ClaimsIdentity)ctx.Principal!.Identity!;

        var dbContext = ctx.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var tenant = await dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == tenantId);
        var roleClaimName = tenant?.Detail?.RoleClaimName;

        var subClaim = ctx.Principal?.FindFirst("sub")?.Value
            ?? ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(subClaim))
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Sub == subClaim);

            if (user != null)
            {
                if (user.IsBanned)
                {
                    logger.LogWarning("[OnTokenValidated] User {UserId} is banned", user.Id);
                    ctx.Fail($"This account has been banned. {user.BanReason ?? ""}");
                    return;
                }

                var existingNameId = identity.FindFirst(ClaimTypes.NameIdentifier);
                if (existingNameId != null)
                {
                    identity.RemoveClaim(existingNameId);
                }
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
            }
            else
            {
                if (!tenant.Detail.HasOidcSettings())
                {
                    logger.LogError("[OnTokenValidated] User {Sub} does not exist and OIDC is not configured", subClaim);
                    ctx.Fail("User account not found.");
                    return;
                }
            }
        }

        if (identity.FindFirst("tenant") == null)
        {
            identity.AddClaim(new Claim("tenant", tenantId));
        }

        // Cookieサイズ削減のため必須クレームのみ保持
        var minimalClaims = new List<Claim>();
        var nameId = identity.FindFirst(ClaimTypes.NameIdentifier)
            ?? ctx.Principal.FindFirst(ClaimTypes.NameIdentifier)
            ?? ctx.Principal.FindFirst("sub");
        if (nameId != null)
        {
            minimalClaims.Add(new Claim(ClaimTypes.NameIdentifier, nameId.Value));
        }

        var email = ctx.Principal.FindFirst(ClaimTypes.Email) ?? ctx.Principal.FindFirst("email");
        if (email != null)
        {
            minimalClaims.Add(new Claim(ClaimTypes.Email, email.Value));
        }

        var name = ctx.Principal.FindFirst(ClaimTypes.Name)
            ?? ctx.Principal.FindFirst("name")
            ?? ctx.Principal.FindFirst("preferred_username");
        if (name != null)
        {
            minimalClaims.Add(new Claim(ClaimTypes.Name, name.Value));
        }

        if (!string.IsNullOrEmpty(roleClaimName))
        {
            var roleClaims = ctx.Principal.FindAll(roleClaimName);
            foreach (var claim in roleClaims)
            {
                minimalClaims.Add(new Claim(ClaimTypes.Role, claim.Value));
            }
        }

        var tenantClaim = ctx.Principal.FindFirst("tenant");
        if (tenantClaim != null)
        {
            minimalClaims.Add(new Claim("tenant", tenantClaim.Value));
        }

        var reducedIdentity = new ClaimsIdentity(
            minimalClaims,
            identity.AuthenticationType,
            ClaimTypes.Name,
            ClaimTypes.Role);
        ctx.Principal = new ClaimsPrincipal(reducedIdentity);
    }

    private static string? ResolveTenantId(AuthenticationProperties? properties, HttpContext httpContext)
    {
        if (properties != null &&
            properties.Items.TryGetValue("tenant", out var tenantId) &&
            !string.IsNullOrWhiteSpace(tenantId))
        {
            return tenantId;
        }

        var tenantFromQuery = httpContext.Request.Query["tenant"].ToString();
        return string.IsNullOrWhiteSpace(tenantFromQuery) ? null : tenantFromQuery;
    }
}
