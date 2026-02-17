using System.ComponentModel.DataAnnotations;

namespace OpenLibraryRent.Models;

/// <summary>
/// 書籍個体の状態
/// </summary>
public enum BookCopyStatus
{
    /// <summary>
    /// 利用可能
    /// </summary>
    Available = 0,

    /// <summary>
    /// 貸出中
    /// </summary>
    Borrowed = 1,

    /// <summary>
    /// メンテナンス中
    /// </summary>
    Maintenance = 2,

    /// <summary>
    /// 紛失
    /// </summary>
    Lost = 3,

    /// <summary>
    /// 廃棄
    /// </summary>
    Discarded = 4
}

/// <summary>
/// 書籍個体
/// 同じISBNの本の冊数管理用
/// </summary>
public class BookCopy : BaseModel
{
    /// <summary>
    /// 親書籍ID
    /// </summary>
    public Guid BookId { get; set; }

    /// <summary>
    /// 親書籍
    /// </summary>
    public Book? Book { get; set; }

    /// <summary>
    /// 管理番号（個体識別用）
    /// </summary>
    [Required]
    [StringLength(50)]
    public string InventoryCode { get; set; } = null!;

    /// <summary>
    /// 状態
    /// </summary>
    public BookCopyStatus Status { get; set; } = BookCopyStatus.Available;

    /// <summary>
    /// 備考（状態説明など）
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// 現在の貸出
    /// </summary>
    public Rental? CurrentRental { get; set; }

    /// <summary>
    /// 貸出履歴
    /// </summary>
    public ICollection<RentalHistory>? RentalHistories { get; set; }
}
