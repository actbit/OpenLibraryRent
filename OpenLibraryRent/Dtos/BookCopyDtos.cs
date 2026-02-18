namespace OpenLibraryRent.Dtos;

/// <summary>
/// 現在の貸出情報
/// </summary>
public class CurrentRentalDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime BorrowedAt { get; set; }
}

/// <summary>
/// 書籍コピー一覧アイテム
/// </summary>
public class BookCopyListItemDto
{
    public Guid Id { get; set; }
    public string? InventoryCode { get; set; }
    public string Status { get; set; } = null!;
    public string? Notes { get; set; }
    public CurrentRentalDto? CurrentRental { get; set; }
}

/// <summary>
/// 書籍コピー詳細
/// </summary>
public class BookCopyDetailDto
{
    public Guid Id { get; set; }
    public string? InventoryCode { get; set; }
    public string Status { get; set; } = null!;
    public string? Notes { get; set; }
    public BookSummaryDto Book { get; set; } = null!;
    public CurrentRentalDto? CurrentRental { get; set; }
}

/// <summary>
/// 書籍コピー作成結果
/// </summary>
public class BookCopyCreateResultDto
{
    public Guid Id { get; set; }
    public string? InventoryCode { get; set; }
    public string Status { get; set; } = null!;
}
