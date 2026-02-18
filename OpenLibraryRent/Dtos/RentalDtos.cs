namespace OpenLibraryRent.Dtos;

/// <summary>
/// 貸出書籍情報
/// </summary>
public class RentalBookDto
{
    public Guid Id { get; set; }
    public string? Isbn { get; set; }
    public string? Title { get; set; }
    public string? CoverImageUrl { get; set; }
}

/// <summary>
/// 貸出書籍コピー情報
/// </summary>
public class RentalBookCopyDto
{
    public Guid Id { get; set; }
    public string? InventoryCode { get; set; }
}

/// <summary>
/// 貸出ユーザー情報
/// </summary>
public class RentalUserDto
{
    public Guid Id { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
}

/// <summary>
/// 自分の貸出情報
/// </summary>
public class MyRentalDto
{
    public Guid Id { get; set; }
    public RentalBookDto Book { get; set; } = null!;
    public RentalBookCopyDto BookCopy { get; set; } = null!;
    public DateTime BorrowedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = null!;
    public int OverdueDays { get; set; }
}

/// <summary>
/// 管理者用貸出情報
/// </summary>
public class AdminRentalDto
{
    public Guid Id { get; set; }
    public RentalBookDto Book { get; set; } = null!;
    public RentalBookCopyDto BookCopy { get; set; } = null!;
    public RentalUserDto User { get; set; } = null!;
    public DateTime BorrowedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = null!;
    public int OverdueDays { get; set; }
}

/// <summary>
/// 延滞貸出情報
/// </summary>
public class OverdueRentalDto
{
    public Guid Id { get; set; }
    public RentalBookDto Book { get; set; } = null!;
    public RentalBookCopyDto BookCopy { get; set; } = null!;
    public RentalUserDto User { get; set; } = null!;
    public DateTime BorrowedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public int OverdueDays { get; set; }
}

/// <summary>
/// 貸出結果
/// </summary>
public class BorrowResultDto
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public Guid BookCopyId { get; set; }
    public Guid UserId { get; set; }
    public DateTime BorrowedAt { get; set; }
    public DateTime? DueDate { get; set; }
}

/// <summary>
/// 返却結果
/// </summary>
public class ReturnResultDto
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public Guid BookCopyId { get; set; }
    public Guid UserId { get; set; }
    public DateTime BorrowedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime ReturnedAt { get; set; }
    public int OverdueDays { get; set; }
}

/// <summary>
/// 自分の返却結果
/// </summary>
public class MyReturnResultDto
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public DateTime ReturnedAt { get; set; }
    public int OverdueDays { get; set; }
}

/// <summary>
/// 貸出履歴アイテム
/// </summary>
public class RentalHistoryItemDto
{
    public Guid Id { get; set; }
    public RentalBookDto Book { get; set; } = null!;
    public DateTime BorrowedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime ReturnedAt { get; set; }
    public int OverdueDays { get; set; }
}

/// <summary>
/// 貸出履歴レスポンス
/// </summary>
public class RentalHistoryResponse
{
    public List<RentalHistoryItemDto> History { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
