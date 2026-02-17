using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenLibraryRent.Models;
using OpenLibraryRent.Services;

namespace OpenLibraryRent.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class TenantsController : BaseController
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TenantManagementService _tenantService;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        ApplicationDbContext dbContext,
        TenantManagementService tenantService,
        ILogger<TenantsController> logger)
    {
        _dbContext = dbContext;
        _tenantService = tenantService;
        _logger = logger;
    }

    /// <summary>
    /// 現在のテナント情報を取得
    /// </summary>
    [HttpGet("current")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCurrentTenant()
    {
        var tenantIdentifier = HttpContext.GetRouteValue("tenant")?.ToString();

        if (string.IsNullOrEmpty(tenantIdentifier))
        {
            return NotFound(new { message = "Tenant not found" });
        }

        var tenant = await _dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == tenantIdentifier);

        if (tenant == null)
        {
            return NotFound(new { message = "Tenant not found" });
        }

        return Ok(new
        {
            id = tenant.Id,
            identifier = tenant.Identifier,
            name = tenant.Name,
            hasOidc = tenant.Detail?.HasOidcSettings() ?? false,
            loanPeriodDays = tenant.Detail?.LoanPeriodDays ?? 14,
            maxLoansPerUser = tenant.Detail?.MaxLoansPerUser ?? 5
        });
    }

    /// <summary>
    /// テナントの貸出設定を更新（管理者用）
    /// </summary>
    [HttpPut("settings")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateTenantSettingsRequest request)
    {
        var tenantIdentifier = HttpContext.GetRouteValue("tenant")?.ToString();

        if (string.IsNullOrEmpty(tenantIdentifier))
        {
            return NotFound(new { message = "Tenant not found" });
        }

        try
        {
            await _tenantService.UpdateLoanSettingsAsync(
                tenantIdentifier,
                request.LoanPeriodDays,
                request.MaxLoansPerUser,
                request.EnableOverdueNotification);

            return Ok(new { message = "Settings updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tenant settings");
            return BadRequest(new { message = ex.Message });
        }
    }
}

public class UpdateTenantSettingsRequest
{
    public int LoanPeriodDays { get; set; } = 14;
    public int MaxLoansPerUser { get; set; } = 5;
    public bool EnableOverdueNotification { get; set; } = true;
}
