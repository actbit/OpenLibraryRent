using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenLibraryRent.Models;
using OpenLibraryRent.Permissions;

namespace OpenLibraryRent.Services;

/// <summary>
/// テナント管理サービス
/// </summary>
public class TenantManagementService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<TenantManagementService> _logger;

    /// <summary>
    /// 1メールアドレスあたりの最大テナント作成数
    /// </summary>
    public const int MaxTenantsPerEmail = 3;

    public TenantManagementService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<TenantManagementService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// メールアドレスごとのテナント作成数を取得
    /// </summary>
    public async Task<int> GetTenantCountByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Tenants
            .CountAsync(t => t.CreatorEmail == email, cancellationToken);
    }

    /// <summary>
    /// テナントを作成（オープン作成用）
    /// </summary>
    public async Task<ApplicationTenantInfo> CreateTenantAsync(
        string identifier,
        string? name,
        string creatorEmail,
        CancellationToken cancellationToken = default)
    {
        // 作成数制限チェック
        var currentCount = await GetTenantCountByEmailAsync(creatorEmail, cancellationToken);
        if (currentCount >= MaxTenantsPerEmail)
        {
            throw new InvalidOperationException($"Maximum number of tenants ({MaxTenantsPerEmail}) reached for this email address");
        }

        // 既存チェック
        var existing = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Identifier == identifier, cancellationToken);

        if (existing != null)
        {
            throw new InvalidOperationException($"Tenant with identifier '{identifier}' already exists");
        }

        var tenant = new ApplicationTenantInfo(identifier, name)
        {
            CreatorEmail = creatorEmail
        };

        var detail = new ApplicationTenantDetail
        {
            TenantId = tenant.Id,
            Tenant = tenant,
            LoanPeriodDays = 14,
            MaxLoansPerUser = 5,
            EnableOverdueNotification = true
        };

        tenant.Detail = detail;

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created tenant: {Identifier} ({Id}) by {Email}", identifier, tenant.Id, creatorEmail);

        return tenant;
    }

    /// <summary>
    /// テナントを作成（システム管理者用 - 作成数制限なし）
    /// </summary>
    public async Task<ApplicationTenantInfo> CreateTenantByAdminAsync(
        string identifier,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        // 既存チェック
        var existing = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Identifier == identifier, cancellationToken);

        if (existing != null)
        {
            throw new InvalidOperationException($"Tenant with identifier '{identifier}' already exists");
        }

        var tenant = new ApplicationTenantInfo(identifier, name);

        var detail = new ApplicationTenantDetail
        {
            TenantId = tenant.Id!,
            Tenant = tenant,
            LoanPeriodDays = 14,
            MaxLoansPerUser = 5,
            EnableOverdueNotification = true
        };

        tenant.Detail = detail;

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created tenant by admin: {Identifier} ({Id})", identifier, tenant.Id);

        return tenant;
    }

    /// <summary>
    /// ユーザーをテナントの管理者として設定
    /// </summary>
    public async Task AssignUserAsAdminAsync(
        Guid userId,
        string tenantIdentifier,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // ユーザーのテナントを更新
        user.TenantId = tenantIdentifier;

        // Adminロールを取得または作成
        var adminRole = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.TenantId == tenantIdentifier && r.Name == "Admin", cancellationToken);

        if (adminRole == null)
        {
            adminRole = new ApplicationRole
            {
                Name = "Admin",
                TenantId = tenantIdentifier,
                Description = "テナント管理者"
            };

            // 管理者権限を付与
            var adminPermissions = new[]
            {
                TenantPermissions.UserRead,
                TenantPermissions.UserManage,
                TenantPermissions.RoleRead,
                TenantPermissions.RoleManage,
                TenantPermissions.BookRead,
                TenantPermissions.BookManage,
                TenantPermissions.RentalRead,
                TenantPermissions.RentalManage,
                TenantPermissions.OverdueRead,
                TenantPermissions.OverdueManage
            };

            foreach (var permission in adminPermissions)
            {
                adminRole.Permissions.Add(new RolePermission
                {
                    Name = permission,
                    RoleId = adminRole.Id
                });
            }

            _dbContext.Roles.Add(adminRole);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // ユーザーロールを割り当て
        var userRole = new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>
        {
            UserId = userId,
            RoleId = adminRole.Id
        };

        _dbContext.UserRoles.Add(userRole);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Assigned user {UserId} as Admin of tenant {TenantId}", userId, tenantIdentifier);
    }

    /// <summary>
    /// テナントを削除
    /// </summary>
    public async Task DeleteTenantAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == identifier, cancellationToken);

        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant with identifier '{identifier}' not found");
        }

        if (tenant.Detail != null)
        {
            _dbContext.TenantDetails.Remove(tenant.Detail);
        }

        _dbContext.Tenants.Remove(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted tenant: {Identifier}", identifier);
    }

    /// <summary>
    /// テナントのOIDC設定を更新
    /// </summary>
    public async Task UpdateOidcSettingsAsync(
        string identifier,
        string? authority,
        string? clientId,
        string? clientSecret,
        string? roleClaimName = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == identifier, cancellationToken);

        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant with identifier '{identifier}' not found");
        }

        tenant.Detail ??= new ApplicationTenantDetail { TenantId = tenant.Id };

        tenant.Detail.OpenIdConnectAuthority = authority;
        tenant.Detail.OpenIdConnectClientId = clientId;

        if (!string.IsNullOrEmpty(clientSecret))
        {
            // ClientSecretは暗号化して保存（必要に応じて）
            tenant.Detail.OpenIdConnectClientSecret = clientSecret;
        }

        tenant.Detail.RoleClaimName = roleClaimName;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated OIDC settings for tenant: {Identifier}", identifier);
    }

    /// <summary>
    /// テナントの貸出設定を更新
    /// </summary>
    public async Task UpdateLoanSettingsAsync(
        string identifier,
        int loanPeriodDays,
        int maxLoansPerUser,
        bool enableOverdueNotification,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == identifier, cancellationToken);

        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant with identifier '{identifier}' not found");
        }

        tenant.Detail ??= new ApplicationTenantDetail { TenantId = tenant.Id };

        tenant.Detail.LoanPeriodDays = loanPeriodDays;
        tenant.Detail.MaxLoansPerUser = maxLoansPerUser;
        tenant.Detail.EnableOverdueNotification = enableOverdueNotification;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated loan settings for tenant: {Identifier}", identifier);
    }

    /// <summary>
    /// テナントのメール制限設定を更新
    /// </summary>
    public async Task UpdateEmailRestrictionSettingsAsync(
        string identifier,
        bool restrictEmailLogin,
        string? allowedEmailDomains,
        string? allowedEmails,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == identifier, cancellationToken);

        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant with identifier '{identifier}' not found");
        }

        tenant.Detail ??= new ApplicationTenantDetail { TenantId = tenant.Id! };

        tenant.Detail.RestrictEmailLogin = restrictEmailLogin;
        tenant.Detail.AllowedEmailDomains = allowedEmailDomains;
        tenant.Detail.AllowedEmails = allowedEmails;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated email restriction settings for tenant: {Identifier}", identifier);
    }

    /// <summary>
    /// テナントのユーザー登録設定を更新
    /// </summary>
    public async Task UpdateRegistrationSettingsAsync(
        string identifier,
        int registrationMode,
        string? allowedEmailDomains,
        string? allowedEmails,
        string? approvalFormFields,
        string? approvalInstructions,
        string? defaultApprovedRoles,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == identifier, cancellationToken);

        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant with identifier '{identifier}' not found");
        }

        tenant.Detail ??= new ApplicationTenantDetail { TenantId = tenant.Id! };

        tenant.Detail.RegistrationMode = (UserRegistrationMode)registrationMode;
        tenant.Detail.AllowedEmailDomains = allowedEmailDomains;
        tenant.Detail.AllowedEmails = allowedEmails;
        tenant.Detail.ApprovalFormFields = approvalFormFields;
        tenant.Detail.ApprovalInstructions = approvalInstructions;
        tenant.Detail.DefaultApprovedRoles = defaultApprovedRoles;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated registration settings for tenant: {Identifier}, Mode: {Mode}", identifier, (UserRegistrationMode)registrationMode);
    }
}
