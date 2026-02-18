using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OpenLibraryRent.Models;

namespace OpenLibraryRent.Services;

/// <summary>
/// OIDCユーザー同期サービス
/// </summary>
public class UserSyncService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UserSyncService> _logger;

    public UserSyncService(
        ApplicationDbContext dbContext,
        ILogger<UserSyncService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// OIDCプロバイダーからのユーザー情報を同期
    /// </summary>
    /// <returns>同期が成功したかどうか。メール制限で拒否された場合はfalse。</returns>
    public async Task<bool> SyncUserAsync(ClaimsPrincipal? principal)
    {
        if (principal == null)
            return false;

        var subClaim = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(subClaim))
        {
            _logger.LogWarning("No sub claim found in principal");
            return false;
        }

        var tenantId = principal.FindFirst("tenant")?.Value;
        if (string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning("No tenant claim found in principal");
            return false;
        }

        var email = principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value;

        // テナント設定を取得してメール制限をチェック
        var tenant = await _dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == tenantId);

        if (tenant?.Detail != null && !tenant.Detail.IsEmailAllowed(email))
        {
            _logger.LogWarning("Email {Email} is not allowed for tenant {TenantId}", email, tenantId);
            return false;
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Sub == subClaim && u.TenantId == tenantId);

        var name = principal.FindFirst(ClaimTypes.Name)?.Value
            ?? principal.FindFirst("name")?.Value
            ?? principal.FindFirst("preferred_username")?.Value;

        if (user == null)
        {
            // 新規ユーザー作成
            user = new ApplicationUser
            {
                Sub = subClaim,
                TenantId = tenantId,
                UserName = email ?? subClaim,
                Email = email,
                DisplayName = name,
                EmailConfirmed = !string.IsNullOrEmpty(email)
            };

            _dbContext.Users.Add(user);
            _logger.LogInformation("Created new user: {Sub} for tenant: {TenantId}", subClaim, tenantId);
        }
        else
        {
            // 既存ユーザー情報を更新
            if (!string.IsNullOrEmpty(email) && user.Email != email)
            {
                user.Email = email;
                user.UserName = email;
            }

            if (!string.IsNullOrEmpty(name) && user.DisplayName != name)
            {
                user.DisplayName = name;
            }

            _logger.LogInformation("Updated user: {Sub} for tenant: {TenantId}", subClaim, tenantId);
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }
}
