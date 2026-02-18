using Finbuckle.MultiTenant;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenLibraryRent.Authentication;
using OpenLibraryRent.Constants;
using OpenLibraryRent.Extensions;
using OpenLibraryRent.Middleware;
using OpenLibraryRent.Models;
using OpenLibraryRent.Repositories;
using OpenLibraryRent.Services;
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

        // PostgreSQL接続文字列
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? builder.Configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Database connection string not configured");

        // DbContext
        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);
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
            .AddOpenIdConnectConfiguration(builder.Configuration, builder.Environment)
            // テナント作成用のMicrosoft OAuth
            .AddMicrosoftAccount("Microsoft", options =>
            {
                var clientId = builder.Configuration["Authentication:Microsoft:ClientId"];
                var clientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];

                if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
                {
                    options.ClientId = clientId;
                    options.ClientSecret = clientSecret;
                    options.CallbackPath = "/auth/microsoft-callback";
                    options.SaveTokens = false;
                }
            });

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

        // サービス登録
        builder.Services.AddSingleton<EncryptionService>();
        builder.Services.AddScoped<UserSyncService>();
        builder.Services.AddScoped<TenantManagementService>();
        builder.Services.AddScoped<RentalService>();
        builder.Services.AddScoped<SystemInitializer>();

        // HttpClient
        builder.Services.AddHttpClient<OpenLibraryService>();

        // MemoryCache
        builder.Services.AddMemoryCache();

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

                    return new BadRequestObjectResult(new
                    {
                        message = "Invalid request",
                        errors
                    });
                };
            });

        mvcBuilder.AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.JsonSerializerOptions.MaxDepth = 128;
        });

        // CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("development", policy =>
            {
                var configuredOrigins = builder.Configuration.GetSection("Cors:DevelopmentOrigins").Get<string[]>() ?? [];
                if (configuredOrigins.Length == 0)
                {
                    configuredOrigins = ["http://localhost:5173", "http://localhost:5000", "http://localhost:5001"];
                }

                policy
                    .WithOrigins(configuredOrigins)
                    .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH")
                    .AllowAnyHeader()
                    .AllowCredentials();
            });

            options.AddPolicy("production", policy =>
            {
                var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                if (configuredOrigins.Length == 0)
                {
                    throw new InvalidOperationException(
                        "CORS:AllowedOrigins is not configured for production.");
                }

                policy
                    .WithOrigins(configuredOrigins)
                    .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH")
                    .WithHeaders("Content-Type", "Authorization")
                    .AllowCredentials();
            });
        });

        // OpenAPI
        builder.Services.AddOpenApi();

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

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUi(options =>
            {
                options.DocumentPath = "openapi/v1.json";
            });
        }

        app.UseHttpsRedirection();
        app.UseRouting();

        app.UseMiddleware<InvalidCookieCleanupMiddleware>();
        app.UseMultiTenant();

        var corsPolicy = app.Environment.IsDevelopment() ? "development" : "production";
        app.UseCors(corsPolicy);

        app.UseAuthentication();
        app.UseAuthorization();

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
