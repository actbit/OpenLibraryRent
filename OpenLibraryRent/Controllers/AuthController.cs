using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using OpenLibraryRent.Constants;
using OpenLibraryRent.Dtos;
using OpenLibraryRent.Extensions;

namespace OpenLibraryRent.Controllers;

[ApiController]
[Route("{tenant}/[controller]")]
public class AuthController : BaseController
{
    private readonly ILogger<AuthController> _logger;

    public AuthController(ILogger<AuthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// ログインページ（OIDCへリダイレクト）
    /// </summary>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        var tenant = HttpContext.GetRouteValue("tenant")?.ToString();

        if (!User.Identity?.IsAuthenticated ?? true)
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = returnUrl ?? $"/{tenant}"
            };

            return Challenge(properties, "oidc");
        }

        // 既に認証済みの場合はリダイレクト
        if (!string.IsNullOrEmpty(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return Redirect($"/{tenant}");
    }

    /// <summary>
    /// 現在のユーザー情報を取得
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserInfoDto> Me()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var name = User.FindFirst(ClaimTypes.Name)?.Value;
        var tenant = User.FindFirst(AuthenticationConstants.TenantClaimType)?.Value;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        return Ok(new UserInfoDto
        {
            UserId = userId,
            Email = email,
            Name = name,
            Tenant = tenant,
            Roles = roles,
            IsAuthenticated = User.Identity?.IsAuthenticated ?? false
        });
    }

    /// <summary>
    /// 認証状態を確認
    /// </summary>
    [HttpGet("check")]
    [AllowAnonymous]
    public ActionResult<AuthStatusDto> Check()
    {
        return Ok(new AuthStatusDto
        {
            IsAuthenticated = User.Identity?.IsAuthenticated ?? false
        });
    }

    /// <summary>
    /// ログアウト
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<MessageResponse>> Logout()
    {
        var tenant = HttpContext.GetRouteValue("tenant")?.ToString();

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        _logger.LogInformation("User logged out from tenant: {Tenant}", tenant);

        return Ok(new MessageResponse("Logged out successfully"));
    }
}
