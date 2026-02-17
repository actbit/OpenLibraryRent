using System.ComponentModel.DataAnnotations;

namespace OpenLibraryRent.Models;

/// <summary>
/// 貸出履歴
/// 返却完了時の履歴レコード
/// </summary>
public class RentalHistory : BaseModel
{
    /// <summary>
    /// 元の貸出ID（参照用）
    /// </summary>
    public Guid OriginalRentalId { get; set; }

    /// <summary>
    /// 書籍ID
    /// </summary>
    public Guid BookId { get; set; }

    /// <summary>
    /// 書籍
    /// </summary>
    public Book? Book { get; set; }

    /// <summary>
    /// 書籍個体ID
    /// </summary>
    public Guid BookCopyId { get; set; }

    /// <summary>
    /// 書籍個体
    /// </summary>
    public BookCopy? BookCopy { get; set; }

    /// <summary>
    /// 借りたユーザーID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 借りたユーザー
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// 借りた日時
    /// </summary>
    public DateTime BorrowedAt { get; set; }

    /// <summary>
    /// 返却期限
    /// </summary>
    public DateTime DueDate { get; set; }

    /// <summary>
    /// 返却日時
    /// </summary>
    public DateTime ReturnedAt { get; set; }

    /// <summary>
    /// 延滞日数
    /// </summary>
    public int OverdueDays { get; set; }

    /// <summary>
    /// 備考
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}
