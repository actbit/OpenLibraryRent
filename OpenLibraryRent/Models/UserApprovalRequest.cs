using System.ComponentModel.DataAnnotations;

namespace OpenLibraryRent.Models;

/// <summary>
/// ユーザー承認リクエスト
/// テナントへの参加申請
/// </summary>
public class UserApprovalRequest
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// テナントID
    /// </summary>
    public string TenantId { get; set; } = null!;

    /// <summary>
    /// 申請者のメールアドレス
    /// </summary>
    [StringLength(255)]
    public string Email { get; set; } = null!;

    /// <summary>
    /// OIDC Subject Identifier
    /// </summary>
    [StringLength(255)]
    public string? Sub { get; set; }

    /// <summary>
    /// 申請者の表示名
    /// </summary>
    [StringLength(100)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// 申請データ（JSON形式、任意の情報）
    /// 管理者が定義したフィールドに対する回答
    /// </summary>
    public string? ApplicationData { get; set; }

    /// <summary>
    /// ステータス
    /// </summary>
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    /// <summary>
    /// 申請日時
    /// </summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 承認・却下日時
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// 承認・却下したユーザーID
    /// </summary>
    public Guid? ProcessedBy { get; set; }

    /// <summary>
    /// 却下理由
    /// </summary>
    [StringLength(500)]
    public string? RejectionReason { get; set; }

    /// <summary>
    /// 承認時に付与するロール（JSON配列）
    /// </summary>
    public string? AssignedRoles { get; set; }

    /// <summary>
    /// 承認時にユーザーに付与するメタデータ（JSON）
    /// </summary>
    public string? UserMetadata { get; set; }

    /// <summary>
    /// 作成されたユーザーID（承認後）
    /// </summary>
    public Guid? CreatedUserId { get; set; }
}

/// <summary>
/// 承認ステータス
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// 承認待ち
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 承認済み
    /// </summary>
    Approved = 1,

    /// <summary>
    /// 却下
    /// </summary>
    Rejected = 2
}
