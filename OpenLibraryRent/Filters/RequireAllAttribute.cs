using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using OpenLibraryRent.Models;
using OpenLibraryRent.Permissions;

namespace OpenLibraryRent.Filters;

/// <summary>
/// すべての権限を持っているかチェック（AND条件）
/// システム権限（system.*）はシステム管理テナントのユーザーのみ
/// </summary>
/// <example>
/// [RequireAll("tenant.user.read", "tenant.user.manage")]
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequireAllAttribute : Attribute, IAsyncActionFilter
{
    private readonly string[] _permissions;

    public RequireAllAttribute(params string[] permissions)
    {
        _permissions = permissions ?? Array.Empty<string>();
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<RequireAllAttribute>>();
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

        // システム権限が必要な場合、システム管理テナントでなければならない
        if (systemPermissions.Count > 0 && tenantId != SystemPermissions.SystemTenantIdentifier)
        {
            logger.LogWarning(
                "[RequireAll] System permission denied: Tenant={Tenant} is not system tenant",
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
            logger.LogWarning("[RequireAll] No roles found: Tenant={Tenant}", tenantId);
            context.Result = new ForbidResult();
            return;
        }

        // 権限を取得
        var userPermissions = await dbContext.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Name)
            .ToListAsync();

        var userPermissionSet = new HashSet<string>(userPermissions);

        // すべての権限を持っているかチェック
        var missingPermissions = _permissions.Where(p => !userPermissionSet.Contains(p)).ToList();

        if (missingPermissions.Count > 0)
        {
            logger.LogWarning(
                "[RequireAll] Permission denied: Tenant={Tenant}, Missing={Missing}, Required={Required}",
                tenantId, string.Join(", ", missingPermissions), string.Join(", ", _permissions));
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
