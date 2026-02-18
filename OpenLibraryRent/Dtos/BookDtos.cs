namespace OpenLibraryRent.Dtos;

/// <summary>
/// 書籍一覧アイテム
/// </summary>
public class BookListItemDto
{
    public Guid Id { get; set; }
    public string? Isbn { get; set; }
    public string? Title { get; set; }
    public string? Authors { get; set; }
    public string? Publisher { get; set; }
    public int? PublishYear { get; set; }
    public string? CoverImageUrl { get; set; }
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
}

/// <summary>
/// 書籍一覧レスポンス
/// </summary>
public class BookListResponse
{
    public List<BookListItemDto> Books { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 書籍コピー情報
/// </summary>
public class BookCopyDto
{
    public Guid Id { get; set; }
    public string? InventoryCode { get; set; }
    public string Status { get; set; } = null!;
    public string? Notes { get; set; }
}

/// <summary>
/// 書籍詳細
/// </summary>
public class BookDetailDto
{
    public Guid Id { get; set; }
    public string? Isbn { get; set; }
    public string? Title { get; set; }
    public string? Authors { get; set; }
    public string? Publisher { get; set; }
    public int? PublishYear { get; set; }
    public int? PageCount { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Description { get; set; }
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public List<BookCopyDto>? Copies { get; set; }
}

/// <summary>
/// 書籍作成結果
/// </summary>
public class BookCreateResultDto
{
    public Guid Id { get; set; }
    public string? Isbn { get; set; }
    public string? Title { get; set; }
}

/// <summary>
/// Open Library書籍データ
/// </summary>
public class OpenLibraryBookDto
{
    public string? Isbn { get; set; }
    public string? Title { get; set; }
    public string? Authors { get; set; }
    public string? Publisher { get; set; }
    public int? PublishYear { get; set; }
    public int? PageCount { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// 書籍競合レスポンス
/// </summary>
public class BookConflictDto
{
    public string Message { get; set; } = null!;
    public Guid BookId { get; set; }
}
