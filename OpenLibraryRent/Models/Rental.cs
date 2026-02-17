using System.ComponentModel.DataAnnotations;

namespace OpenLibraryRent.Models;

/// <summary>
/// 貸出状態
/// </summary>
public enum RentalStatus
{
    /// <summary>
    /// 貸出中
    /// </summary>
    Active = 0,

    /// <summary>
    /// 返却済み
    /// </summary>
    Returned = 1,

    /// <summary>
    /// 延滞中
    /// </summary>
    Overdue = 2
}

/// <summary>
/// 現在の貸出
/// </summary>
public class Rental : BaseModel
{
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
    public DateTime? ReturnedAt { get; set; }

    /// <summary>
    /// 状態
    /// </summary>
    public RentalStatus Status { get; set; } = RentalStatus.Active;

    /// <summary>
    /// 備考
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// 延滞日数（計算用）
    /// </summary>
    public int OverdueDays => Status == RentalStatus.Overdue || (Status == RentalStatus.Active && DateTime.UtcNow > DueDate)
        ? (int)(DateTime.UtcNow - DueDate).TotalDays
        : 0;

    /// <summary>
    /// 延滞かどうか
    /// </summary>
    public bool IsOverdue => Status == RentalStatus.Active && DateTime.UtcNow > DueDate;
}
