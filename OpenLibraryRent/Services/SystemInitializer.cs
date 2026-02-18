using Microsoft.EntityFrameworkCore;
using OpenLibraryRent.Models;
using OpenLibraryRent.Permissions;

namespace OpenLibraryRent.Services;

/// <summary>
/// システム初期化サービス
/// システム管理テナントと初期ロール・権限を作成
/// </summary>
public class SystemInitializer
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<SystemInitializer> _logger;

    public SystemInitializer(ApplicationDbContext dbContext, ILogger<SystemInitializer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// システム管理テナントを初期化
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // システムテナントが存在するか確認
        var systemTenant = await _dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == SystemPermissions.SystemTenantIdentifier, cancellationToken);

        if (systemTenant == null)
        {
            _logger.LogInformation("Creating system tenant...");

            systemTenant = new ApplicationTenantInfo(
                SystemPermissions.SystemTenantIdentifier,
                "System Administration");

            var detail = new ApplicationTenantDetail
            {
                TenantId = systemTenant.Id,
                Tenant = systemTenant,
                LoanPeriodDays = 14,
                MaxLoansPerUser = 5,
                EnableOverdueNotification = false,
                RestrictEmailLogin = false
            };

            systemTenant.Detail = detail;

            _dbContext.Tenants.Add(systemTenant);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("System tenant created: {Id}", systemTenant.Id);
        }

        // システム管理者ロールを作成
        await CreateSystemAdminRoleAsync(systemTenant, cancellationToken);
    }

    private async Task CreateSystemAdminRoleAsync(ApplicationTenantInfo tenant, CancellationToken cancellationToken)
    {
        var adminRoleName = "SystemAdmin";

        var existingRole = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.TenantId == tenant.Identifier && r.Name == adminRoleName, cancellationToken);

        if (existingRole == null)
        {
            _logger.LogInformation("Creating SystemAdmin role...");

            var role = new ApplicationRole
            {
                Name = adminRoleName,
                TenantId = tenant.Identifier!,
                Description = "システム管理者 - 全テナントの管理権限"
            };

            // 全システム権限を付与
            var permissions = new[]
            {
                SystemPermissions.TenantCreate,
                SystemPermissions.TenantRead,
                SystemPermissions.TenantManage,
                SystemPermissions.TenantDelete,
                SystemPermissions.SettingsRead,
                SystemPermissions.SettingsManage
            };

            foreach (var permission in permissions)
            {
                role.Permissions.Add(new RolePermission
                {
                    Name = permission,
                    RoleId = role.Id
                });
            }

            _dbContext.Roles.Add(role);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("SystemAdmin role created with all system permissions");
        }
    }
}
