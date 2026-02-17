using System.ComponentModel.DataAnnotations;

namespace OpenLibraryRent.Models;

/// <summary>
/// RLS対応の基本モデル
/// すべてのテナント分離が必要なエンティティの基底クラス
/// </summary>
public class BaseModel
{
    public BaseModel()
    {
        Id = Guid.CreateVersion7();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    [Key]
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// テナントID（RLSで使用）
    /// </summary>
    public string TenantId { get; set; } = null!;
}
