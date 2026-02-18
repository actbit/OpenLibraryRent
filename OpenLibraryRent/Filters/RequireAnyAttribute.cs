using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using OpenLibraryRent.Models;
using OpenLibraryRent.Permissions;

namespace OpenLibraryRent.Filters;

/// <summary>
/// 権限のいずれかを持っているかチェック（OR条件）
/// システム権限（system.*）はシステム管理テナントのユーザーのみ
/// </summary>
/// <example>
/// [RequireAny("tenant.book.read", "tenant.book.manage")]
/// [RequireAny("system.tenant.create")]
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequireAnyAttribute : Attribute, IAsyncActionFilter
{
    private readonly string[] _permissions;

    public RequireAnyAttribute(params string[] permissions)
    {
        _permissions = permissions ?? Array.Empty<string>();
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<RequireAnyAttribute>>();
        var dbContext = httpContext.RequestServices.GetRequiredService<ApplicationDbContext>();

        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var tenantId = user.FindFirst("tenant")?.Value;
        if (string.IsNullOrEmpty(tenantId))
        {
            context.Result = new ForbidResult();
            return;
        }

        // システム権限のチェック
        var systemPermissions = _permissions.Where(p => p.StartsWith("system.")).ToList();
        var tenantPermissions = _permissions.Where(p => p.StartsWith("tenant.")).ToList();

        // システム権限が必要な場合、システム管理テナントでなければならない
        if (systemPermissions.Count > 0 && tenantId != SystemPermissions.SystemTenantIdentifier)
        {
            logger.LogWarning(
                "[RequireAny] System permission denied: Tenant={Tenant} is not system tenant",
                tenantId);
            context.Result = new ForbidResult();
            return;
        }

        // ユーザーのロールを取得
        var userRoles = user.FindAll(System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        // ロールIDを取得
        var roleIds = await dbContext.Roles
            .Where(r => r.TenantId == tenantId && userRoles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync();

        if (roleIds.Count == 0)
        {
            logger.LogWarning("[RequireAny] No roles found: Tenant={Tenant}", tenantId);
            context.Result = new ForbidResult();
            return;
        }

        // 権限を取得
        var userPermissions = await dbContext.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Name)
            .ToListAsync();

        // いずれかの権限を持っているかチェック
        foreach (var permission in _permissions)
        {
            if (userPermissions.Contains(permission))
            {
                await next();
                return;
            }
        }

        logger.LogWarning(
            "[RequireAny] Permission denied: Tenant={Tenant}, Required={Required}, Has={Has}",
            tenantId, string.Join(", ", _permissions), string.Join(", ", userPermissions));
        context.Result = new ForbidResult();
    }
}
