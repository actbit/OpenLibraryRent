using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace OpenLibraryRent.Authentication;

/// <summary>
/// CookieAuthenticationOptionsを設定
/// </summary>
internal class CookieAuthenticationConfiguration : IPostConfigureOptions<CookieAuthenticationOptions>
{
    private readonly IServiceProvider _serviceProvider;

    public CookieAuthenticationConfiguration(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (name != CookieAuthenticationDefaults.AuthenticationScheme)
            return;

        var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
        var useTenantAwareCookies = configuration.GetValue<bool>("Authentication:UseTenantAwareCookies");

        if (useTenantAwareCookies)
        {
            var httpContextAccessor = _serviceProvider.GetRequiredService<IHttpContextAccessor>();
            var logger = _serviceProvider.GetRequiredService<ILogger<TenantAwareCookieManager>>();
            options.CookieManager = new TenantAwareCookieManager(httpContextAccessor, logger);
        }
        else
        {
            options.CookieManager = new ChunkingCookieManager();
        }
    }
}
