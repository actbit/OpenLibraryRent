using System.ComponentModel.DataAnnotations;

namespace OpenLibraryRent.Models;

/// <summary>
/// 書籍マスタ
/// ISBNをキーに書籍情報を管理
/// </summary>
public class Book : BaseModel
{
    /// <summary>
    /// ISBN-10またはISBN-13
    /// </summary>
    [Required]
    [StringLength(20)]
    public string Isbn { get; set; } = null!;

    /// <summary>
    /// 書籍タイトル
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Title { get; set; } = null!;

    /// <summary>
    /// 著者（カンマ区切りで複数可）
    /// </summary>
    [StringLength(1000)]
    public string? Authors { get; set; }

    /// <summary>
    /// 出版社
    /// </summary>
    [StringLength(200)]
    public string? Publisher { get; set; }

    /// <summary>
    /// 出版年
    /// </summary>
    public int? PublishYear { get; set; }

    /// <summary>
    /// ページ数
    /// </summary>
    public int? PageCount { get; set; }

    /// <summary>
    /// 表紙画像URL
    /// </summary>
    [StringLength(1000)]
    public string? CoverImageUrl { get; set; }

    /// <summary>
    /// 書籍の説明
    /// </summary>
    [StringLength(5000)]
    public string? Description { get; set; }

    /// <summary>
    /// 総所蔵数（キャッシュ用）
    /// </summary>
    public int TotalCopies { get; set; }

    /// <summary>
    /// 利用可能数（キャッシュ用）
    /// </summary>
    public int AvailableCopies { get; set; }

    /// <summary>
    /// 書籍個体のコレクション
    /// </summary>
    public ICollection<BookCopy>? Copies { get; set; }

    /// <summary>
    /// 貸出のコレクション
    /// </summary>
    public ICollection<Rental>? Rentals { get; set; }
}
