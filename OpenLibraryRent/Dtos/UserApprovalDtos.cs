namespace OpenLibraryRent.Dtos;

/// <summary>
/// 承認設定
/// </summary>
public class ApprovalSettingsDto
{
    public bool RequireApproval { get; set; }
    public string? ApprovalFormFields { get; set; }
    public string? ApprovalInstructions { get; set; }
    public string? DefaultApprovedRoles { get; set; }
}

/// <summary>
/// 承認リクエスト一覧アイテム
/// </summary>
public class ApprovalRequestListItemDto
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string Status { get; set; } = null!;
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>
/// 承認リクエスト詳細
/// </summary>
public class ApprovalRequestDetailDto
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? Sub { get; set; }
    public string? DisplayName { get; set; }
    public string? ApplicationData { get; set; }
    public string Status { get; set; } = null!;
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public Guid? ProcessedBy { get; set; }
    public string? RejectionReason { get; set; }
    public string? AssignedRoles { get; set; }
    public string? UserMetadata { get; set; }
}

/// <summary>
/// 承認結果
/// </summary>
public class ApprovalResultDto
{
    public string Message { get; set; } = null!;
    public Guid UserId { get; set; }
}

/// <summary>
/// 申請結果
/// </summary>
public class ApplicationResultDto
{
    public string Message { get; set; } = null!;
    public Guid RequestId { get; set; }
}

/// <summary>
/// 申請ステータス
/// </summary>
public class ApplicationStatusDto
{
    public string Status { get; set; } = null!;
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? RejectionReason { get; set; }
}
