using Finbuckle.MultiTenant;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using OpenLibraryRent.Authentication;
using OpenLibraryRent.Constants;
using OpenLibraryRent.Dtos;
using OpenLibraryRent.Extensions;
using OpenLibraryRent.Middleware;
using OpenLibraryRent.Models;
using OpenLibraryRent.Repositories;
using OpenLibraryRent.Services;
using OpenLibraryRent.Services.Caching;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace OpenLibraryRent;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        // DataProtection設定
        var keysDir = Path.Combine(builder.Environment.ContentRootPath, ".keys");
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
            .SetApplicationName("OpenLibraryRent");

        // DbContext (Aspire統合を使用 - 接続文字列はAspireから自動注入)
        builder.AddNpgsqlDbContext<ApplicationDbContext>("openlibraryrent-db", configure =>
        {
            configure.DisableRetry = false;
        });

        // Identity
        builder.Services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>();

        builder.Services.AddHttpContextAccessor();

        // 認証設定
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = builder.Configuration["Authentication:CookieName"] ?? "OpenLibraryRent.Cookie";
                options.LoginPath = AuthenticationConstants.Paths.LoginPath;
                options.LogoutPath = AuthenticationConstants.Paths.LogoutPath;
                options.ExpireTimeSpan = TimeSpan.FromHours(AuthenticationConstants.Cookie.ExpirationHours);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;

                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = ctx =>
                    {
                        if (ctx.Request.IsApiRequest())
                        {
                            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        }

                        var tenantId = ctx.Request.RouteValues[AuthenticationConstants.TenantClaimType]?.ToString();
                        if (string.IsNullOrEmpty(tenantId))
                        {
                            var pathValue = ctx.Request.Path.Value;
                            if (!string.IsNullOrEmpty(pathValue))
                            {
                                var segments = pathValue.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                                if (segments.Length > 0)
                                {
                                    var firstSegment = segments[0];
                                    if (!string.IsNullOrWhiteSpace(firstSegment) &&
                                        firstSegment.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                                    {
                                        tenantId = firstSegment;
                                    }
                                }
                            }
                        }

                        var loginPath = string.IsNullOrEmpty(tenantId)
                            ? AuthenticationConstants.Paths.LoginPath
                            : $"/{tenantId}{AuthenticationConstants.Paths.LoginPath}";

                        var currentPath = ctx.Request.Path.Value ?? string.Empty;
                        var currentQuery = ctx.Request.QueryString.Value ?? string.Empty;
                        var fullReturnUrl = currentPath + currentQuery;

                        if (!string.IsNullOrEmpty(fullReturnUrl) && fullReturnUrl != "/" && !fullReturnUrl.Contains("/auth/login"))
                        {
                            loginPath += $"?returnUrl={Uri.EscapeDataString(fullReturnUrl)}";
                        }

                        ctx.Response.Redirect(loginPath);
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = ctx =>
                    {
                        if (ctx.Request.IsApiRequest())
                        {
                            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        }
                        ctx.Response.Redirect(ctx.RedirectUri);
                        return Task.CompletedTask;
                    },
                    OnSigningIn = async context =>
                    {
                        var tenantId = context.HttpContext.GetRouteValue("tenant")?.ToString();
                        if (string.IsNullOrEmpty(tenantId))
                        {
                            tenantId = context.HttpContext.Request.Query[AuthenticationConstants.TenantClaimType].ToString();
                        }
                        if (string.IsNullOrEmpty(tenantId))
                        {
                            var pathValue = context.HttpContext.Request?.Path.Value;
                            if (!string.IsNullOrEmpty(pathValue))
                            {
                                var segments = pathValue.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                                if (segments.Length > 0)
                                {
                                    var firstSegment = segments[0];
                                    if (!string.IsNullOrWhiteSpace(firstSegment) &&
                                        firstSegment.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                                    {
                                        tenantId = firstSegment;
                                    }
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(tenantId))
                        {
                            context.HttpContext.Items[AuthenticationConstants.Cookie.TenantForCookieKey] = tenantId;
                            context.Properties.Items[AuthenticationConstants.TenantClaimType] = tenantId;
                            context.Properties.IsPersistent = true;
                        }
                    }
                };
            })
            .AddOpenIdConnectConfiguration(builder.Configuration, builder.Environment);

        // テナント作成用のMicrosoft OAuth（設定されている場合のみ登録）
        var microsoftClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
        var microsoftClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
        if (!string.IsNullOrEmpty(microsoftClientId) && !string.IsNullOrEmpty(microsoftClientSecret))
        {
            builder.Services.AddAuthentication()
                .AddMicrosoftAccount("Microsoft", options =>
                {
                    options.ClientId = microsoftClientId;
                    options.ClientSecret = microsoftClientSecret;
                    options.CallbackPath = "/auth/microsoft-callback";
                    options.SaveTokens = false;
                });
        }

        builder.Services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(
                CookieAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
        });

        builder.Services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, CookieAuthenticationConfiguration>();

        // MultiTenant設定
        var usePerTenantAuthentication = builder.Configuration.GetValue<bool>("Authentication:UsePerTenantAuthentication");

        var multiTenantBuilder = builder.Services
            .AddMultiTenant<ApplicationTenantInfo>()
            .WithStrategy<CustomClaimStrategy>(ServiceLifetime.Singleton, "tenant")
            .WithStore<EFCoreMultiTenantStore>(ServiceLifetime.Scoped);

        if (usePerTenantAuthentication)
        {
            multiTenantBuilder.WithPerTenantAuthentication();
        }

        // 暗号化キー設定
        var masterEncryptionKey = builder.Configuration["Encryption:Key"]
            ?? Environment.GetEnvironmentVariable("ENCRYPTION_KEY");

        if (string.IsNullOrWhiteSpace(masterEncryptionKey) && !builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Encryption key is not configured. " +
                "Please set the 'Encryption:Key' configuration value or the 'ENCRYPTION_KEY' environment variable.");
        }

        // 暗号化サービスのインスタンス（ConfigurePerTenantで使用）
        var masterEncryptionForOidc = new EncryptionService(
            masterEncryptionKey ?? string.Empty,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        // テナントごとのOIDC設定
        builder.Services.ConfigurePerTenant<OpenIdConnectOptions, ApplicationTenantInfo>(
            AuthenticationConstants.OidcSchemeName,
            (options, tenantInfo) =>
            {
                var tenantDetail = tenantInfo.Detail;
                if (tenantDetail != null &&
                    !string.IsNullOrEmpty(tenantDetail.OpenIdConnectAuthority) &&
                    !string.IsNullOrEmpty(tenantDetail.OpenIdConnectClientId))
                {
                    if (!Uri.TryCreate(tenantDetail.OpenIdConnectAuthority, UriKind.Absolute, out var authorityUri) ||
                        (authorityUri.Scheme != Uri.UriSchemeHttps && !builder.Environment.IsDevelopment()))
                    {
                        return; // このテナント設定をスキップ
                    }

                    options.Authority = tenantDetail.OpenIdConnectAuthority;
                    options.ClientId = tenantDetail.OpenIdConnectClientId;

                    options.Configuration = new OpenIdConnectConfiguration
                    {
                        AuthorizationEndpoint = tenantDetail.OpenIdConnectAuthorizationEndpoint,
                        TokenEndpoint = tenantDetail.OpenIdConnectTokenEndpoint,
                        JwksUri = tenantDetail.OpenIdConnectJwksUri,
                        EndSessionEndpoint = tenantDetail.OpenIdConnectEndSessionEndpoint,
                        Issuer = tenantDetail.OpenIdConnectAuthority
                    };

                    options.TokenValidationParameters.ValidIssuer = tenantDetail.OpenIdConnectAuthority;
                    options.TokenValidationParameters.ValidateIssuer = true;
                    options.TokenValidationParameters.ValidAudience = tenantDetail.OpenIdConnectClientId;
                    options.TokenValidationParameters.ValidateAudience = true;

                    // ClientSecretの復号化
                    if (!string.IsNullOrEmpty(tenantDetail.TenantEncryptionKey) &&
                        !string.IsNullOrEmpty(tenantDetail.OpenIdConnectClientSecret))
                    {
                        try
                        {
                            var decryptedSecret = masterEncryptionForOidc.DecryptWithTenantKey(
                                tenantDetail.TenantEncryptionKey,
                                tenantDetail.OpenIdConnectClientSecret);

                            if (!string.IsNullOrEmpty(decryptedSecret))
                            {
                                options.ClientSecret = decryptedSecret;
                            }
                        }
                        catch (Exception)
                        {
                            // 復号化に失敗した場合はスキップ
                        }
                    }
                }
                // OIDC設定がない場合はデフォルト値（placeholder-client-id）を維持
            });

        // サービス登録
        builder.Services.AddSingleton<EncryptionService>();
        builder.Services.AddScoped<UserSyncService>();
        builder.Services.AddScoped<TenantManagementService>();
        builder.Services.AddScoped<RentalService>();
        builder.Services.AddScoped<SystemInitializer>();

        // HttpClient
        builder.Services.AddHttpClient<OpenLibraryService>();

        // Cache Service（Redisが設定されていればRedis、そうでなければインメモリ）
        builder.Services.AddCacheService(builder.Configuration);

        // Repositories
        builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));

        // MVC
        var mvcBuilder = builder.Services
            .AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(kvp => kvp.Value?.Errors != null && kvp.Value.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value!.Errors
                                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage)
                                .ToArray()
                        );

                    return new BadRequestObjectResult(new ValidationErrorResponse
                    {
                        Message = "Invalid request",
                        Errors = errors
                    });
                };
            });

        mvcBuilder.AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.JsonSerializerOptions.MaxDepth = 128;
        });

        // CORS
        // Aspire環境（OTEL_EXPORTER_OTLP_ENDPOINTが設定されている）または開発環境では開発用CORSを使用
        var isAspireEnvironment = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        var isDevelopment = builder.Environment.IsDevelopment() || isAspireEnvironment;

        builder.Services.AddCors(options =>
        {
            if (isDevelopment)
            {
                // 開発時: 全許可
                options.AddPolicy("default", policy =>
                {
                    policy
                        .SetIsOriginAllowed(_ => true)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            }
            else
            {
                // 本番時: 環境変数で固定
                var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                if (configuredOrigins.Length == 0)
                {
                    throw new InvalidOperationException(
                        "CORS:AllowedOrigins is not configured for production.");
                }

                options.AddPolicy("default", policy =>
                {
                    policy
                        .WithOrigins(configuredOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            }
        });

        // OpenAPI (開発時のみ)
        if (isDevelopment)
        {
            builder.Services.AddOpenApi();
        }

        var app = builder.Build();

        // データベースマイグレーション
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    await dbContext.Database.MigrateAsync();
                    logger.LogInformation("Database migrations applied successfully");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during database migration");
                throw;
            }

            // システム初期化
            try
            {
                var systemInitializer = scope.ServiceProvider.GetRequiredService<SystemInitializer>();
                await systemInitializer.InitializeAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during system initialization");
            }
        }

        app.MapDefaultEndpoints();

        if (isDevelopment)
        {
            app.MapOpenApi();
            app.UseSwaggerUi(options =>
            {
                options.DocumentPath = "openapi/v1.json";
            });
        }

        app.UseHttpsRedirection();
        app.UseRouting();

        // 認証・MultiTenantを必要とするパスのみに適用
        app.UseWhen(context =>
        {
            var path = context.Request.Path.Value ?? string.Empty;

            // APIパスまたはテナント付きパスのみ認証・MultiTenantを適用
            // rootページ、静的ファイル、Swagger等はスキップ
            if (path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/alive", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/_app", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // ルートパス（/）はスキップ
            if (path == "/" || string.IsNullOrEmpty(path.Trim('/')))
            {
                return false;
            }

            return true;
        }, appBuilder =>
        {
            appBuilder.UseMiddleware<InvalidCookieCleanupMiddleware>();
            appBuilder.UseMultiTenant();
            appBuilder.UseAuthentication();
            appBuilder.UseAuthorization();
        });

        app.UseCors("default");

        app.MapControllers();

        // 静的ファイル配信
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapFallback(async context =>
        {
            if (AuthenticationConstants.Paths.IsApiPath(context.Request.Path.Value))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "index.html"));
        });

        app.Run();
    }
}
