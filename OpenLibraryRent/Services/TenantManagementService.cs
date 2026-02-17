using Microsoft.EntityFrameworkCore;
using OpenLibraryRent.Models;

namespace OpenLibraryRent.Services;

/// <summary>
/// テナント管理サービス
/// </summary>
public class TenantManagementService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<TenantManagementService> _logger;

    public TenantManagementService(
        ApplicationDbContext dbContext,
        ILogger<TenantManagementService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// テナントを作成
    /// </summary>
    public async Task<ApplicationTenantInfo> CreateTenantAsync(
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
            TenantId = tenant.Id,
            Tenant = tenant,
            LoanPeriodDays = 14,
            MaxLoansPerUser = 5,
            EnableOverdueNotification = true
        };

        tenant.Detail = detail;

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created tenant: {Identifier} ({Id})", identifier, tenant.Id);

        return tenant;
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
}
