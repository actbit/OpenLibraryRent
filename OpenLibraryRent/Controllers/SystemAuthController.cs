using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace OpenLibraryRent.Controllers;

/// <summary>
/// システム認証（テナント作成用Microsoft認証）
/// テナント個別のOIDCとは独立して動作
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SystemAuthController : ControllerBase
{
    private readonly ILogger<SystemAuthController> _logger;

    public SystemAuthController(ILogger<SystemAuthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Microsoft ログイン開始（テナント作成用）
    /// </summary>
    [HttpGet("microsoft-login")]
    [AllowAnonymous]
    public IActionResult MicrosoftLogin([FromQuery] string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated ?? false)
        {
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return Redirect("/create-tenant");
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = returnUrl ?? "/create-tenant"
        };

        return Challenge(properties, "Microsoft");
    }

    /// <summary>
    /// 現在のユーザー情報を取得（テナント作成用）
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var name = User.FindFirst(ClaimTypes.Name)?.Value;

        return Ok(new
        {
            userId,
            email,
            name,
            isAuthenticated = User.Identity?.IsAuthenticated ?? false
        });
    }

    /// <summary>
    /// 認証状態を確認
    /// </summary>
    [HttpGet("check")]
    [AllowAnonymous]
    public IActionResult Check()
    {
        return Ok(new
        {
            isAuthenticated = User.Identity?.IsAuthenticated ?? false,
            email = User.FindFirst(ClaimTypes.Email)?.Value
        });
    }

    /// <summary>
    /// ログアウト
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        _logger.LogInformation("User logged out from system auth");

        return Ok(new { message = "Logged out successfully" });
    }
}
