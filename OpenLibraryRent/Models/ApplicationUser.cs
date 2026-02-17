using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace OpenLibraryRent.Models;

/// <summary>
/// アプリケーションユーザー
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
    }

    /// <summary>
    /// OIDC Subject Identifier
    /// </summary>
    [StringLength(255)]
    public string? Sub { get; set; }

    /// <summary>
    /// テナントID（RLS用）
    /// </summary>
    public string TenantId { get; set; } = null!;

    /// <summary>
    /// 表示名
    /// </summary>
    [StringLength(100)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// BAN状態
    /// </summary>
    public bool IsBanned { get; set; }

    /// <summary>
    /// BAN理由
    /// </summary>
    [StringLength(500)]
    public string? BanReason { get; set; }
}

/// <summary>
/// アプリケーションロール
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
        Id = Guid.CreateVersion7();
    }

    /// <summary>
    /// テナントID（RLS用）
    /// </summary>
    public string TenantId { get; set; } = null!;

    /// <summary>
    /// ロールの説明
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }
}
