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
    public async Task SyncUserAsync(ClaimsPrincipal? principal)
    {
        if (principal == null)
            return;

        var subClaim = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(subClaim))
        {
            _logger.LogWarning("No sub claim found in principal");
            return;
        }

        var tenantId = principal.FindFirst("tenant")?.Value;
        if (string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning("No tenant claim found in principal");
            return;
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Sub == subClaim && u.TenantId == tenantId);

        var email = principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value;

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
    }
}
