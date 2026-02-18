using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenLibraryRent.Dtos;
using OpenLibraryRent.Filters;
using OpenLibraryRent.Models;
using OpenLibraryRent.Permissions;

namespace OpenLibraryRent.Controllers;

/// <summary>
/// ユーザー承認管理
/// </summary>
[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class UserApprovalController : BaseController
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UserApprovalController> _logger;

    public UserApprovalController(
        ApplicationDbContext dbContext,
        ILogger<UserApprovalController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// テナントの承認設定を取得
    /// </summary>
    [HttpGet("settings")]
    [RequireAny("tenant.user.manage")]
    public async Task<ActionResult<ApprovalSettingsDto>> GetSettings()
    {
        var tenantId = User.FindFirst("tenant")?.Value;
        if (string.IsNullOrEmpty(tenantId))
            return BadRequest(new MessageResponse("Tenant not found"));

        var detail = await _dbContext.TenantDetails
            .FirstOrDefaultAsync(d => d.TenantId == tenantId);

        if (detail == null)
            return NotFound(new MessageResponse("Tenant settings not found"));

        return Ok(new ApprovalSettingsDto
        {
            RequireApproval = detail.RequireApproval,
            ApprovalFormFields = detail.ApprovalFormFields,
            ApprovalInstructions = detail.ApprovalInstructions,
            DefaultApprovedRoles = detail.DefaultApprovedRoles
        });
    }

    /// <summary>
    /// テナントの承認設定を更新
    /// </summary>
    [HttpPut("settings")]
    [RequireAny("tenant.user.manage")]
    public async Task<ActionResult<MessageResponse>> UpdateSettings([FromBody] UpdateApprovalSettingsRequest request)
    {
        var tenantId = User.FindFirst("tenant")?.Value;
        if (string.IsNullOrEmpty(tenantId))
            return BadRequest(new MessageResponse("Tenant not found"));

        var detail = await _dbContext.TenantDetails
            .FirstOrDefaultAsync(d => d.TenantId == tenantId);

        if (detail == null)
            return NotFound(new MessageResponse("Tenant settings not found"));

        detail.RequireApproval = request.RequireApproval;
        detail.ApprovalFormFields = request.ApprovalFormFields;
        detail.ApprovalInstructions = request.ApprovalInstructions;
        detail.DefaultApprovedRoles = request.DefaultApprovedRoles;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Approval settings updated for tenant: {TenantId}", tenantId);

        return Ok(new MessageResponse("Settings updated successfully"));
    }

    /// <summary>
    /// 承認待ちの一覧を取得
    /// </summary>
    [HttpGet("requests")]
    [RequireAny("tenant.user.read", "tenant.user.manage")]
    public async Task<ActionResult<List<ApprovalRequestListItemDto>>> ListRequests([FromQuery] ApprovalStatus? status = null)
    {
        var tenantId = User.FindFirst("tenant")?.Value;
        if (string.IsNullOrEmpty(tenantId))
            return BadRequest(new MessageResponse("Tenant not found"));

        var query = _dbContext.UserApprovalRequests
            .Where(r => r.TenantId == tenantId);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var requests = await query
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new ApprovalRequestListItemDto
            {
                Id = r.Id,
                Email = r.Email,
                DisplayName = r.DisplayName,
                Status = r.Status.ToString(),
                RequestedAt = r.RequestedAt,
                ProcessedAt = r.ProcessedAt,
                RejectionReason = r.RejectionReason
            })
            .ToListAsync();

        return Ok(requests);
    }

    /// <summary>
    /// 承認リクエストの詳細を取得
    /// </summary>
    [HttpGet("requests/{id}")]
    [RequireAny("tenant.user.read", "tenant.user.manage")]
    public async Task<ActionResult<ApprovalRequestDetailDto>> GetRequest(Guid id)
    {
        var tenantId = User.FindFirst("tenant")?.Value;
        if (string.IsNullOrEmpty(tenantId))
            return BadRequest(new MessageResponse("Tenant not found"));

        var request = await _dbContext.UserApprovalRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);

        if (request == null)
            return NotFound(new MessageResponse("Request not found"));

        return Ok(new ApprovalRequestDetailDto
        {
            Id = request.Id,
            Email = request.Email,
            Sub = request.Sub,
            DisplayName = request.DisplayName,
            ApplicationData = request.ApplicationData,
            Status = request.Status.ToString(),
            RequestedAt = request.RequestedAt,
            ProcessedAt = request.ProcessedAt,
            ProcessedBy = request.ProcessedBy,
            RejectionReason = request.RejectionReason,
            AssignedRoles = request.AssignedRoles,
            UserMetadata = request.UserMetadata
        });
    }

    /// <summary>
    /// 承認リクエストを承認
    /// </summary>
    [HttpPost("requests/{id}/approve")]
    [RequireAny("tenant.user.manage")]
    public async Task<ActionResult<ApprovalResultDto>> Approve(Guid id, [FromBody] ApproveUserRequest? request = null)
    {
        var tenantId = User.FindFirst("tenant")?.Value;
        var userId = GetCurrentUserId();

        if (string.IsNullOrEmpty(tenantId) || userId == null)
            return BadRequest(new MessageResponse("Invalid context"));

        var approvalRequest = await _dbContext.UserApprovalRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);

        if (approvalRequest == null)
            return NotFound(new MessageResponse("Request not found"));

        if (approvalRequest.Status != ApprovalStatus.Pending)
            return BadRequest(new MessageResponse("Request already processed"));

        // ユーザーを作成
        var user = new ApplicationUser
        {
            Email = approvalRequest.Email,
            UserName = approvalRequest.Email,
            DisplayName = request?.DisplayName ?? approvalRequest.DisplayName,
            Sub = approvalRequest.Sub,
            TenantId = tenantId
        };

        _dbContext.Users.Add(user);

        // 承認情報を更新
        approvalRequest.Status = ApprovalStatus.Approved;
        approvalRequest.ProcessedAt = DateTime.UtcNow;
        approvalRequest.ProcessedBy = userId;
        approvalRequest.CreatedUserId = user.Id;

        if (request != null)
        {
            approvalRequest.AssignedRoles = request.AssignedRoles;
            approvalRequest.UserMetadata = request.Metadata;
        }

        await _dbContext.SaveChangesAsync();

        // ロールを割り当て
        var rolesToAssign = request?.AssignedRoles;
        if (string.IsNullOrEmpty(rolesToAssign))
        {
            // デフォルトロールを使用
            var detail = await _dbContext.TenantDetails
                .FirstOrDefaultAsync(d => d.TenantId == tenantId);
            rolesToAssign = detail?.DefaultApprovedRoles;
        }

        if (!string.IsNullOrEmpty(rolesToAssign))
        {
            var roleNames = rolesToAssign.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var roleName in roleNames)
            {
                var role = await _dbContext.Roles
                    .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == roleName);

                if (role != null)
                {
                    _dbContext.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>
                    {
                        UserId = user.Id,
                        RoleId = role.Id
                    });
                }
            }
            await _dbContext.SaveChangesAsync();
        }

        _logger.LogInformation("User approved: {Email} in tenant {TenantId}", approvalRequest.Email, tenantId);

        return Ok(new ApprovalResultDto
        {
            Message = "User approved successfully",
            UserId = user.Id
        });
    }

    /// <summary>
    /// 承認リクエストを却下
    /// </summary>
    [HttpPost("requests/{id}/reject")]
    [RequireAny("tenant.user.manage")]
    public async Task<ActionResult<MessageResponse>> Reject(Guid id, [FromBody] RejectUserRequest request)
    {
        var tenantId = User.FindFirst("tenant")?.Value;
        var userId = GetCurrentUserId();

        if (string.IsNullOrEmpty(tenantId) || userId == null)
            return BadRequest(new MessageResponse("Invalid context"));

        var approvalRequest = await _dbContext.UserApprovalRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);

        if (approvalRequest == null)
            return NotFound(new MessageResponse("Request not found"));

        if (approvalRequest.Status != ApprovalStatus.Pending)
            return BadRequest(new MessageResponse("Request already processed"));

        approvalRequest.Status = ApprovalStatus.Rejected;
        approvalRequest.ProcessedAt = DateTime.UtcNow;
        approvalRequest.ProcessedBy = userId;
        approvalRequest.RejectionReason = request.Reason;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User rejected: {Email} in tenant {TenantId}", approvalRequest.Email, tenantId);

        return Ok(new MessageResponse("Request rejected"));
    }

    /// <summary>
    /// 申請を送信（未認証ユーザー用）
    /// </summary>
    [HttpPost("apply")]
    [AllowAnonymous]
    public async Task<ActionResult<ApplicationResultDto>> Apply([FromBody] ApplyForApprovalRequest request)
    {
        var tenantId = HttpContext.GetRouteValue("tenant")?.ToString();
        if (string.IsNullOrEmpty(tenantId))
            return BadRequest(new MessageResponse("Tenant not found"));

        // テナントの設定を確認
        var detail = await _dbContext.TenantDetails
            .FirstOrDefaultAsync(d => d.TenantId == tenantId);

        if (detail == null || detail.RegistrationMode != UserRegistrationMode.Approval)
            return BadRequest(new MessageResponse("Approval not required for this tenant"));

        // 既存の申請を確認
        var existingRequest = await _dbContext.UserApprovalRequests
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Email == request.Email);

        if (existingRequest != null)
        {
            if (existingRequest.Status == ApprovalStatus.Pending)
                return BadRequest(new MessageResponse("Application already pending"));
            if (existingRequest.Status == ApprovalStatus.Approved)
                return BadRequest(new MessageResponse("Already approved"));
        }

        // 新しい申請を作成
        var approvalRequest = new UserApprovalRequest
        {
            TenantId = tenantId,
            Email = request.Email,
            DisplayName = request.DisplayName,
            Sub = request.Sub,
            ApplicationData = request.ApplicationData,
            Status = ApprovalStatus.Pending
        };

        _dbContext.UserApprovalRequests.Add(approvalRequest);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("New approval request: {Email} for tenant {TenantId}", request.Email, tenantId);

        return Ok(new ApplicationResultDto
        {
            Message = "Application submitted successfully",
            RequestId = approvalRequest.Id
        });
    }

    /// <summary>
    /// 申請のステータスを確認
    /// </summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<ActionResult<ApplicationStatusDto>> GetStatus([FromQuery] string email)
    {
        var tenantId = HttpContext.GetRouteValue("tenant")?.ToString();
        if (string.IsNullOrEmpty(tenantId))
            return BadRequest(new MessageResponse("Tenant not found"));

        var request = await _dbContext.UserApprovalRequests
            .Where(r => r.TenantId == tenantId && r.Email == email)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync();

        if (request == null)
            return Ok(new ApplicationStatusDto { Status = "not_applied", RequestedAt = default });

        return Ok(new ApplicationStatusDto
        {
            Status = request.Status.ToString().ToLowerInvariant(),
            RequestedAt = request.RequestedAt,
            ProcessedAt = request.ProcessedAt,
            RejectionReason = request.RejectionReason
        });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }
}

public class UpdateApprovalSettingsRequest
{
    public bool RequireApproval { get; set; }
    public string? ApprovalFormFields { get; set; }
    public string? ApprovalInstructions { get; set; }
    public string? DefaultApprovedRoles { get; set; }
}

public class ApproveUserRequest
{
    public string? DisplayName { get; set; }
    public string? AssignedRoles { get; set; }
    public string? Metadata { get; set; }
}

public class RejectUserRequest
{
    public string? Reason { get; set; }
}

public class ApplyForApprovalRequest
{
    public string Email { get; set; } = null!;
    public string? DisplayName { get; set; }
    public string? Sub { get; set; }
    public string? ApplicationData { get; set; }
}
