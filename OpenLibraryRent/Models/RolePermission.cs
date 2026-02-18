using System.ComponentModel.DataAnnotations.Schema;

namespace OpenLibraryRent.Models;

/// <summary>
/// ロール権限
/// </summary>
public class RolePermission
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// 権限名（例: tenant.book.read, tenant.user.manage）
    /// </summary>
    public string Name { get; set; } = null!;

    [ForeignKey(nameof(Role))]
    public Guid RoleId { get; set; }
    public ApplicationRole Role { get; set; } = null!;
}
