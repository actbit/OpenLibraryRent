using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenLibraryRent.Dtos;
using OpenLibraryRent.Filters;
using OpenLibraryRent.Models;
using OpenLibraryRent.Permissions;
using OpenLibraryRent.Services;

namespace OpenLibraryRent.Controllers;

/// <summary>
/// テナント管理
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TenantManagementService _tenantService;
    private readonly EncryptionService _encryptionService;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        ApplicationDbContext dbContext,
        TenantManagementService tenantService,
        EncryptionService encryptionService,
        ILogger<TenantsController> logger)
    {
        _dbContext = dbContext;
        _tenantService = tenantService;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    /// <summary>
    /// テナント作成の制限情報を取得
    /// </summary>
    [HttpGet("creation-limit")]
    [Authorize]  // Googleログイン必須
    public async Task<ActionResult<TenantLimitCheckDto>> GetCreationLimit()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            return BadRequest(new MessageResponse("Email claim not found"));
        }

        var currentCount = await _tenantService.GetTenantCountByEmailAsync(email);
        var maxCount = TenantManagementService.MaxTenantsPerEmail;

        return Ok(new TenantLimitCheckDto
        {
            Email = email,
            CurrentCount = currentCount,
            MaxCount = maxCount,
            Remaining = maxCount - currentCount,
            CanCreate = currentCount < maxCount
        });
    }

    /// <summary>
    /// テナントを作成（Googleログイン済みユーザー向け）
    /// </summary>
    [HttpPost("create")]
    [Authorize]  // Googleログイン必須
    public async Task<IActionResult> CreatePublic([FromBody] CreateTenantPublicRequest request)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            return BadRequest(new MessageResponse("Email claim not found. Please login with Google."));
        }

        if (string.IsNullOrEmpty(request.Identifier))
        {
            return BadRequest(new MessageResponse("Identifier is required"));
        }

        // 識別子のバリデーション
        if (!request.Identifier.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
        {
            return BadRequest(new MessageResponse("Identifier can only contain letters, numbers, hyphens, and underscores"));
        }

        if (request.Identifier.Length < 3 || request.Identifier.Length > 50)
        {
            return BadRequest(new MessageResponse("Identifier must be between 3 and 50 characters"));
        }

        // システム予約識別子のチェック
        var reservedIdentifiers = new[] { "system", "admin", "api", "auth", "login", "logout", "www", "app" };
        if (reservedIdentifiers.Contains(request.Identifier.ToLowerInvariant()))
        {
            return BadRequest(new MessageResponse("This identifier is reserved"));
        }

        try
        {
            var tenant = await _tenantService.CreateTenantAsync(
                request.Identifier,
                request.Name ?? request.Identifier,
                email);

            _logger.LogInformation("Tenant created publicly: {Identifier} by {Email}", request.Identifier, email);

            return Ok(new TenantCreatePublicResultDto
            {
                Id = tenant.Id,
                Identifier = tenant.Identifier,
                Name = tenant.Name,
                Message = "Tenant created successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message));
        }
    }

    /// <summary>
    /// テナント一覧を取得（システム管理者のみ）
    /// </summary>
    [HttpGet]
    [RequireAny("system.tenant.read", "system.tenant.manage")]
    public async Task<ActionResult<List<TenantListItemDto>>> List()
    {
        var tenants = await _dbContext.Tenants
            .Include(t => t.Detail)
            .OrderBy(t => t.Identifier)
            .Select(t => new TenantListItemDto
            {
                Id = t.Id,
                Identifier = t.Identifier,
                Name = t.Name,
                HasOidc = t.Detail != null && t.Detail.HasOidcSettings(),
                LoanPeriodDays = t.Detail != null ? t.Detail.LoanPeriodDays : 14,
                MaxLoansPerUser = t.Detail != null ? t.Detail.MaxLoansPerUser : 5,
                UserCount = _dbContext.Users.Count(u => u.TenantId == t.Identifier),
                BookCount = _dbContext.Books.Count(b => b.TenantId == t.Identifier)
            })
            .ToListAsync();

        return Ok(tenants);
    }

    /// <summary>
    /// 識別子でテナント情報を取得（テナント内から使用）
    /// </summary>
    [HttpGet("by-identifier/{identifier}")]
    [AllowAnonymous]
    public async Task<ActionResult<TenantByIdentifierDto>> GetByIdentifier(string identifier)
    {
        var tenant = await _dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == identifier);

        if (tenant == null)
        {
            return NotFound(new MessageResponse("Tenant not found"));
        }

        return Ok(new TenantByIdentifierDto
        {
            Id = tenant.Id,
            Identifier = tenant.Identifier,
            Name = tenant.Name,
            HasOidc = tenant.Detail?.HasOidcSettings() ?? false,
            LoanPeriodDays = tenant.Detail?.LoanPeriodDays ?? 14,
            MaxLoansPerUser = tenant.Detail?.MaxLoansPerUser ?? 5
        });
    }

    /// <summary>
    /// テナント詳細を取得（システム管理者のみ）
    /// </summary>
    [HttpGet("{id}")]
    [RequireAny("system.tenant.read", "system.tenant.manage")]
    public async Task<IActionResult> Get(string id)
    {
        var tenant = await _dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant == null)
        {
            return NotFound(new MessageResponse("Tenant not found"));
        }

        return Ok(new TenantDetailAdminDto
        {
            Id = tenant.Id,
            Identifier = tenant.Identifier,
            Name = tenant.Name,
            Detail = new TenantDetailInfoDto
            {
                LoanPeriodDays = tenant.Detail?.LoanPeriodDays,
                MaxLoansPerUser = tenant.Detail?.MaxLoansPerUser,
                EnableOverdueNotification = tenant.Detail?.EnableOverdueNotification,
                RestrictEmailLogin = tenant.Detail?.RestrictEmailLogin,
                AllowedEmailDomains = tenant.Detail?.AllowedEmailDomains,
                AllowedEmails = tenant.Detail?.AllowedEmails,
                HasOidc = tenant.Detail?.HasOidcSettings() ?? false,
                OpenIdConnectAuthority = tenant.Detail?.OpenIdConnectAuthority,
                OpenIdConnectClientId = tenant.Detail?.OpenIdConnectClientId,
                HasClientSecret = !string.IsNullOrEmpty(tenant.Detail?.OpenIdConnectClientSecret),
                RoleClaimName = tenant.Detail?.RoleClaimName
            }
        });
    }

    /// <summary>
    /// テナントを作成（システム管理者のみ）
    /// </summary>
    [HttpPost]
    [RequireAny("system.tenant.create", "system.tenant.manage")]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request)
    {
        if (string.IsNullOrEmpty(request.Identifier))
        {
            return BadRequest(new MessageResponse("Identifier is required"));
        }

        // 識別子のバリデーション
        if (!request.Identifier.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
        {
            return BadRequest(new MessageResponse("Identifier can only contain letters, numbers, hyphens, and underscores"));
        }

        // システム予約識別子のチェック
        if (request.Identifier.Equals("system", StringComparison.OrdinalIgnoreCase) ||
            request.Identifier.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new MessageResponse("This identifier is reserved"));
        }

        try
        {
            var tenant = await _tenantService.CreateTenantByAdminAsync(
                request.Identifier,
                request.Name ?? request.Identifier);

            // OIDC設定がある場合は追加
            if (!string.IsNullOrEmpty(request.OpenIdConnectAuthority))
            {
                await _tenantService.UpdateOidcSettingsAsync(
                    request.Identifier,
                    request.OpenIdConnectAuthority,
                    request.OpenIdConnectClientId,
                    request.OpenIdConnectClientSecret,
                    request.RoleClaimName);
            }

            _logger.LogInformation("Tenant created: {Identifier}", request.Identifier);

            return Ok(new TenantUpdateResultDto
            {
                Id = tenant.Id,
                Identifier = tenant.Identifier,
                Name = tenant.Name
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MessageResponse(ex.Message));
        }
    }

    /// <summary>
    /// テナントを更新（システム管理者のみ）
    /// </summary>
    [HttpPut("{id}")]
    [RequireAny("system.tenant.manage")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateTenantRequest request)
    {
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null)
        {
            return NotFound(new MessageResponse("Tenant not found"));
        }

        if (!string.IsNullOrEmpty(request.Name))
        {
            tenant.Name = request.Name;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Tenant updated: {Id}", id);

        return Ok(new TenantUpdateResultDto
        {
            Id = tenant.Id,
            Identifier = tenant.Identifier,
            Name = tenant.Name
        });
    }

    /// <summary>
    /// テナントを削除（システム管理者のみ）
    /// </summary>
    [HttpDelete("{id}")]
    [RequireAny("system.tenant.delete", "system.tenant.manage")]
    public async Task<IActionResult> Delete(string id)
    {
        var tenant = await _dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant == null)
        {
            return NotFound(new MessageResponse("Tenant not found"));
        }

        // システムテナントは削除不可
        if (tenant.Identifier == SystemPermissions.SystemTenantIdentifier)
        {
            return BadRequest(new MessageResponse("Cannot delete system tenant"));
        }

        // 関連データの確認
        var userCount = await _dbContext.Users.CountAsync(u => u.TenantId == tenant.Identifier);
        if (userCount > 0)
        {
            return BadRequest(new MessageResponse($"Cannot delete tenant with {userCount} users"));
        }

        try
        {
            await _tenantService.DeleteTenantAsync(tenant.Identifier);
            return Ok(new MessageResponse("Tenant deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete tenant: {Id}", id);
            return BadRequest(new MessageResponse(ex.Message));
        }
    }

    /// <summary>
    /// OIDC設定を更新（システム管理者のみ）
    /// </summary>
    [HttpPut("{id}/oidc")]
    [RequireAny("system.tenant.manage")]
    public async Task<IActionResult> UpdateOidc(string id, [FromBody] UpdateOidcRequest request)
    {
        var tenant = await _dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant == null)
        {
            return NotFound(new MessageResponse("Tenant not found"));
        }

        try
        {
            // ClientSecretの暗号化
            var clientSecret = request.OpenIdConnectClientSecret;
            if (!string.IsNullOrEmpty(clientSecret))
            {
                clientSecret = _encryptionService.Encrypt(clientSecret);
            }

            await _tenantService.UpdateOidcSettingsAsync(
                tenant.Identifier,
                request.OpenIdConnectAuthority,
                request.OpenIdConnectClientId,
                clientSecret,
                request.RoleClaimName);

            return Ok(new MessageResponse("OIDC settings updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update OIDC settings for tenant: {Id}", id);
            return BadRequest(new MessageResponse(ex.Message));
        }
    }

    /// <summary>
    /// 貸出設定を更新（システム管理者のみ）
    /// </summary>
    [HttpPut("{id}/loan-settings")]
    [RequireAny("system.tenant.manage")]
    public async Task<IActionResult> UpdateLoanSettings(string id, [FromBody] UpdateLoanSettingsRequest request)
    {
        var tenant = await _dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant == null)
        {
            return NotFound(new MessageResponse("Tenant not found"));
        }

        try
        {
            await _tenantService.UpdateLoanSettingsAsync(
                tenant.Identifier,
                request.LoanPeriodDays,
                request.MaxLoansPerUser,
                request.EnableOverdueNotification);

            return Ok(new MessageResponse("Loan settings updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update loan settings for tenant: {Id}", id);
            return BadRequest(new MessageResponse(ex.Message));
        }
    }

    /// <summary>
    /// メール制限設定を更新（システム管理者のみ）
    /// </summary>
    [HttpPut("{id}/email-restriction")]
    [RequireAny("system.tenant.manage")]
    public async Task<IActionResult> UpdateEmailRestriction(string id, [FromBody] UpdateEmailRestrictionRequest request)
    {
        var tenant = await _dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant == null)
        {
            return NotFound(new MessageResponse("Tenant not found"));
        }

        try
        {
            await _tenantService.UpdateEmailRestrictionSettingsAsync(
                tenant.Identifier,
                request.RestrictEmailLogin,
                request.AllowedEmailDomains,
                request.AllowedEmails);

            return Ok(new MessageResponse("Email restriction settings updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update email restriction settings for tenant: {Id}", id);
            return BadRequest(new MessageResponse(ex.Message));
        }
    }

    /// <summary>
    /// ユーザー登録設定を更新（システム管理者のみ）
    /// </summary>
    [HttpPut("{id}/registration-settings")]
    [RequireAny("system.tenant.manage")]
    public async Task<IActionResult> UpdateRegistrationSettings(string id, [FromBody] UpdateRegistrationSettingsRequest request)
    {
        var tenant = await _dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant == null)
        {
            return NotFound(new MessageResponse("Tenant not found"));
        }

        try
        {
            await _tenantService.UpdateRegistrationSettingsAsync(
                tenant.Identifier,
                request.RegistrationMode,
                request.AllowedEmailDomains,
                request.AllowedEmails,
                request.ApprovalFormFields,
                request.ApprovalInstructions,
                request.DefaultApprovedRoles);

            return Ok(new MessageResponse("Registration settings updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update registration settings for tenant: {Id}", id);
            return BadRequest(new MessageResponse(ex.Message));
        }
    }
}

public class CreateTenantRequest
{
    public string Identifier { get; set; } = null!;
    public string? Name { get; set; }
    public string? OpenIdConnectAuthority { get; set; }
    public string? OpenIdConnectClientId { get; set; }
    public string? OpenIdConnectClientSecret { get; set; }
    public string? RoleClaimName { get; set; }
}

public class UpdateTenantRequest
{
    public string? Name { get; set; }
}

public class UpdateOidcRequest
{
    public string? OpenIdConnectAuthority { get; set; }
    public string? OpenIdConnectClientId { get; set; }
    public string? OpenIdConnectClientSecret { get; set; }
    public string? RoleClaimName { get; set; }
}

public class UpdateLoanSettingsRequest
{
    public int LoanPeriodDays { get; set; } = 14;
    public int MaxLoansPerUser { get; set; } = 5;
    public bool EnableOverdueNotification { get; set; } = true;
}

public class UpdateEmailRestrictionRequest
{
    public bool RestrictEmailLogin { get; set; }
    public string? AllowedEmailDomains { get; set; }
    public string? AllowedEmails { get; set; }
}

public class CreateTenantPublicRequest
{
    public string Identifier { get; set; } = null!;
    public string? Name { get; set; }
}

public class UpdateRegistrationSettingsRequest
{
    public int RegistrationMode { get; set; }
    public string? AllowedEmailDomains { get; set; }
    public string? AllowedEmails { get; set; }
    public string? ApprovalFormFields { get; set; }
    public string? ApprovalInstructions { get; set; }
    public string? DefaultApprovedRoles { get; set; }
}
