namespace OpenLibraryRent.Dtos;

/// <summary>
/// ユーザー一覧アイテム
/// </summary>
public class UserListItemDto
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? UserName { get; set; }
    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CurrentRentals { get; set; }
    public List<string> Roles { get; set; } = [];
}

/// <summary>
/// ユーザー一覧レスポンス
/// </summary>
public class UserListResponse
{
    public List<UserListItemDto> Users { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 書籍概要（貸出用）
/// </summary>
public class BookSummaryDto
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? Isbn { get; set; }
}

/// <summary>
/// ユーザー貸出情報
/// </summary>
public class UserRentalDto
{
    public Guid Id { get; set; }
    public BookSummaryDto Book { get; set; } = null!;
    public DateTime BorrowedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = null!;
}

/// <summary>
/// ユーザー詳細
/// </summary>
public class UserDetailDto
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? UserName { get; set; }
    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Roles { get; set; } = [];
    public List<UserRentalDto> CurrentRentals { get; set; } = [];
    public int TotalRentals { get; set; }
}

/// <summary>
/// ユーザー更新レスポンス
/// </summary>
public class UserUpdateDto
{
    public Guid Id { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
}

/// <summary>
/// BAN状態レスポンス
/// </summary>
public class BanStatusDto
{
    public Guid Id { get; set; }
    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }
}

/// <summary>
/// ロール一覧アイテム
/// </summary>
public class RoleListItemDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = [];
    public int UserCount { get; set; }
}

/// <summary>
/// ロール作成/更新レスポンス
/// </summary>
public class RoleDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// ロール一覧レスポンス
/// </summary>
public class RolesResponse
{
    public List<string> Roles { get; set; } = [];
}
