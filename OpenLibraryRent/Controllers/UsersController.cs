using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenLibraryRent.Models;

namespace OpenLibraryRent.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class UsersController : BaseController
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<UsersController> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    /// <summary>
    /// ユーザー一覧を取得（管理者用）
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _dbContext.Users.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(u =>
                (u.DisplayName != null && u.DisplayName.Contains(search)) ||
                (u.Email != null && u.Email.Contains(search)) ||
                (u.UserName != null && u.UserName.Contains(search)));
        }

        var total = await query.CountAsync();

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.UserName,
                u.IsBanned,
                u.BanReason,
                u.CreatedAt,
                CurrentRentals = _dbContext.Rentals
                    .Count(r => r.UserId == u.Id && (r.Status == RentalStatus.Active || r.Status == RentalStatus.Overdue))
            })
            .ToListAsync();

        // ロール情報を取得
        var result = new List<object>();
        foreach (var user in users)
        {
            var appUser = await _userManager.FindByIdAsync(user.Id.ToString());
            var roles = appUser != null ? await _userManager.GetRolesAsync(appUser) : [];
            result.Add(new
            {
                user.Id,
                user.Email,
                user.DisplayName,
                user.UserName,
                user.IsBanned,
                user.BanReason,
                user.CreatedAt,
                user.CurrentRentals,
                Roles = roles
            });
        }

        return Ok(new { users = result, total, page, pageSize });
    }

    /// <summary>
    /// ユーザー詳細を取得
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var currentUserId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Librarian");

        // 自分自身または管理者のみアクセス可能
        if (currentUserId != id && !isAdmin)
        {
            return Forbid();
        }

        var user = await _dbContext.Users
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.UserName,
                u.IsBanned,
                u.BanReason,
                u.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        var appUser = await _userManager.FindByIdAsync(id.ToString());
        var roles = appUser != null ? await _userManager.GetRolesAsync(appUser) : [];

        // 貸出状況
        var currentRentals = await _dbContext.Rentals
            .Include(r => r.Book)
            .Where(r => r.UserId == id && (r.Status == RentalStatus.Active || r.Status == RentalStatus.Overdue))
            .Select(r => new
            {
                r.Id,
                Book = new { r.Book!.Id, r.Book.Title, r.Book.Isbn },
                r.BorrowedAt,
                r.DueDate,
                r.Status
            })
            .ToListAsync();

        var totalRentals = await _dbContext.RentalHistories.CountAsync(h => h.UserId == id);

        return Ok(new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.UserName,
            user.IsBanned,
            user.BanReason,
            user.CreatedAt,
            Roles = roles,
            CurrentRentals = currentRentals,
            TotalRentals = totalRentals
        });
    }

    /// <summary>
    /// ユーザー情報を更新
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        var currentUserId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");

        // 自分自身または管理者のみ更新可能
        if (currentUserId != id && !isAdmin)
        {
            return Forbid();
        }

        var user = await _dbContext.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        if (!string.IsNullOrEmpty(request.DisplayName))
        {
            user.DisplayName = request.DisplayName;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User updated: {UserId}", id);

        return Ok(new
        {
            user.Id,
            user.DisplayName,
            user.Email
        });
    }

    /// <summary>
    /// ユーザーをBAN
    /// </summary>
    [HttpPost("{id}/ban")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Ban(Guid id, [FromBody] BanRequest request)
    {
        var user = await _dbContext.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // アクティブな貸出がある場合はBAN不可
        var hasActiveRentals = await _dbContext.Rentals
            .AnyAsync(r => r.UserId == id && (r.Status == RentalStatus.Active || r.Status == RentalStatus.Overdue));

        if (hasActiveRentals)
        {
            return BadRequest(new { message = "Cannot ban user with active rentals" });
        }

        user.IsBanned = true;
        user.BanReason = request.Reason;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User banned: {UserId}, Reason: {Reason}", id, request.Reason);

        return Ok(new
        {
            user.Id,
            user.IsBanned,
            user.BanReason
        });
    }

    /// <summary>
    /// BANを解除
    /// </summary>
    [HttpDelete("{id}/ban")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Unban(Guid id)
    {
        var user = await _dbContext.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        user.IsBanned = false;
        user.BanReason = null;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User unbanned: {UserId}", id);

        return Ok(new
        {
            user.Id,
            user.IsBanned
        });
    }

    /// <summary>
    /// ユーザーのロールを取得
    /// </summary>
    [HttpGet("{id}/roles")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetRoles(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new { roles });
    }

    /// <summary>
    /// ユーザーにロールを割り当て
    /// </summary>
    [HttpPost("{id}/roles")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        var roleExists = await _roleManager.RoleExistsAsync(request.Role);
        if (!roleExists)
        {
            return BadRequest(new { message = "Role not found" });
        }

        var result = await _userManager.AddToRoleAsync(user, request.Role);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        _logger.LogInformation("Role assigned: UserId={UserId}, Role={Role}", id, request.Role);

        return Ok(new { message = "Role assigned successfully" });
    }

    /// <summary>
    /// ユーザーからロールを削除
    /// </summary>
    [HttpDelete("{id}/roles/{role}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveRole(Guid id, string role)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        var result = await _userManager.RemoveFromRoleAsync(user, role);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        _logger.LogInformation("Role removed: UserId={UserId}, Role={Role}", id, role);

        return Ok(new { message = "Role removed successfully" });
    }

    /// <summary>
    /// ロール一覧を取得
    /// </summary>
    [HttpGet("roles")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ListRoles()
    {
        var roles = await _dbContext.Roles
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                UserCount = _dbContext.UserRoles.Count(ur => ur.RoleId == r.Id)
            })
            .ToListAsync();

        return Ok(roles);
    }

    /// <summary>
    /// ロールを作成
    /// </summary>
    [HttpPost("roles")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        if (string.IsNullOrEmpty(request.Name))
        {
            return BadRequest(new { message = "Role name is required" });
        }

        var exists = await _roleManager.RoleExistsAsync(request.Name);
        if (exists)
        {
            return BadRequest(new { message = "Role already exists" });
        }

        var role = new ApplicationRole
        {
            Name = request.Name,
            Description = request.Description
        };

        var result = await _roleManager.CreateAsync(role);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        _logger.LogInformation("Role created: {RoleName}", request.Name);

        return Ok(new { role.Id, role.Name, role.Description });
    }

    /// <summary>
    /// ロールを更新
    /// </summary>
    [HttpPut("roles/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var role = await _dbContext.Roles.FindAsync(id);
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        if (!string.IsNullOrEmpty(request.Description))
        {
            role.Description = request.Description;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Role updated: {RoleId}", id);

        return Ok(new { role.Id, role.Name, role.Description });
    }

    /// <summary>
    /// ロールを削除
    /// </summary>
    [HttpDelete("roles/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        var role = await _dbContext.Roles.FindAsync(id);
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        // ユーザーが割り当てられている場合は削除不可
        var hasUsers = await _dbContext.UserRoles.AnyAsync(ur => ur.RoleId == id);
        if (hasUsers)
        {
            return BadRequest(new { message = "Cannot delete role with assigned users" });
        }

        var result = await _roleManager.DeleteAsync(role);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        _logger.LogInformation("Role deleted: {RoleId}", id);

        return Ok(new { message = "Role deleted successfully" });
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

public class UpdateUserRequest
{
    public string? DisplayName { get; set; }
}

public class BanRequest
{
    public string? Reason { get; set; }
}

public class AssignRoleRequest
{
    public string Role { get; set; } = null!;
}

public class CreateRoleRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

public class UpdateRoleRequest
{
    public string? Description { get; set; }
}
