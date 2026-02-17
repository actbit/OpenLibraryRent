using Microsoft.EntityFrameworkCore;
using OpenLibraryRent.Models;

namespace OpenLibraryRent.Services;

/// <summary>
/// 貸出管理サービス
/// </summary>
public class RentalService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<RentalService> _logger;

    public RentalService(
        ApplicationDbContext dbContext,
        ILogger<RentalService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// 貸出処理
    /// </summary>
    public async Task<Rental> BorrowAsync(
        Guid userId,
        Guid bookCopyId,
        int loanPeriodDays,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        // 書籍個体を取得
        var bookCopy = await _dbContext.BookCopies
            .Include(bc => bc.Book)
            .FirstOrDefaultAsync(bc => bc.Id == bookCopyId, cancellationToken);

        if (bookCopy == null)
        {
            throw new InvalidOperationException("Book copy not found");
        }

        if (bookCopy.Status != BookCopyStatus.Available)
        {
            throw new InvalidOperationException($"Book copy is not available (status: {bookCopy.Status})");
        }

        // ユーザーの現在の貸出数を確認
        var currentRentals = await _dbContext.Rentals
            .CountAsync(r => r.UserId == userId && r.Status == RentalStatus.Active, cancellationToken);

        // 書籍個体を更新
        bookCopy.Status = BookCopyStatus.Borrowed;

        // 貸出を作成
        var rental = new Rental
        {
            BookId = bookCopy.BookId,
            BookCopyId = bookCopyId,
            UserId = userId,
            BorrowedAt = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(loanPeriodDays),
            Status = RentalStatus.Active,
            Notes = notes
        };

        _dbContext.Rentals.Add(rental);

        // 書籍の利用可能数を更新
        var book = bookCopy.Book;
        if (book != null)
        {
            book.AvailableCopies = Math.Max(0, book.AvailableCopies - 1);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Book borrowed: BookCopyId={BookCopyId}, UserId={UserId}, DueDate={DueDate}",
            bookCopyId, userId, rental.DueDate);

        return rental;
    }

    /// <summary>
    /// ISBNで貸出処理（利用可能な個体を自動選択）
    /// </summary>
    public async Task<Rental?> BorrowByIsbnAsync(
        Guid userId,
        string isbn,
        int loanPeriodDays,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        // ISBNで書籍を検索
        var book = await _dbContext.Books
            .FirstOrDefaultAsync(b => b.Isbn == isbn, cancellationToken);

        if (book == null)
        {
            return null;
        }

        // 利用可能な個体を検索
        var availableCopy = await _dbContext.BookCopies
            .FirstOrDefaultAsync(bc => bc.BookId == book.Id && bc.Status == BookCopyStatus.Available, cancellationToken);

        if (availableCopy == null)
        {
            return null;
        }

        return await BorrowAsync(userId, availableCopy.Id, loanPeriodDays, notes, cancellationToken);
    }

    /// <summary>
    /// 返却処理
    /// </summary>
    public async Task<RentalHistory> ReturnAsync(
        Guid rentalId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var rental = await _dbContext.Rentals
            .Include(r => r.Book)
            .Include(r => r.BookCopy)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == rentalId, cancellationToken);

        if (rental == null)
        {
            throw new InvalidOperationException("Rental not found");
        }

        if (rental.Status != RentalStatus.Active && rental.Status != RentalStatus.Overdue)
        {
            throw new InvalidOperationException($"Rental is not active (status: {rental.Status})");
        }

        var returnedAt = DateTime.UtcNow;
        var overdueDays = (int)Math.Max(0, (returnedAt - rental.DueDate).TotalDays);

        // 履歴を作成
        var history = new RentalHistory
        {
            OriginalRentalId = rental.Id,
            BookId = rental.BookId,
            BookCopyId = rental.BookCopyId,
            UserId = rental.UserId,
            BorrowedAt = rental.BorrowedAt,
            DueDate = rental.DueDate,
            ReturnedAt = returnedAt,
            OverdueDays = overdueDays,
            Notes = notes ?? rental.Notes
        };

        _dbContext.RentalHistories.Add(history);

        // 貸出を更新
        rental.Status = RentalStatus.Returned;
        rental.ReturnedAt = returnedAt;
        rental.Notes = notes ?? rental.Notes;

        // 書籍個体を更新
        if (rental.BookCopy != null)
        {
            rental.BookCopy.Status = BookCopyStatus.Available;
        }

        // 書籍の利用可能数を更新
        if (rental.Book != null)
        {
            rental.Book.AvailableCopies++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Book returned: RentalId={RentalId}, OverdueDays={OverdueDays}",
            rentalId, overdueDays);

        return history;
    }

    /// <summary>
    /// ISBNで返却処理
    /// </summary>
    public async Task<RentalHistory?> ReturnByIsbnAsync(
        string isbn,
        Guid userId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        // ISBNで書籍を検索
        var book = await _dbContext.Books
            .FirstOrDefaultAsync(b => b.Isbn == isbn, cancellationToken);

        if (book == null)
        {
            return null;
        }

        // ユーザーの当該書籍のアクティブな貸出を検索
        var rental = await _dbContext.Rentals
            .Include(r => r.Book)
            .Include(r => r.BookCopy)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r =>
                r.BookId == book.Id &&
                r.UserId == userId &&
                (r.Status == RentalStatus.Active || r.Status == RentalStatus.Overdue),
                cancellationToken);

        if (rental == null)
        {
            return null;
        }

        return await ReturnAsync(rental.Id, notes, cancellationToken);
    }

    /// <summary>
    /// 延滞中の貸出一覧を取得
    /// </summary>
    public async Task<List<Rental>> GetOverdueRentalsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await _dbContext.Rentals
            .Include(r => r.Book)
            .Include(r => r.BookCopy)
            .Include(r => r.User)
            .Where(r => r.Status == RentalStatus.Active && r.DueDate < now)
            .OrderBy(r => r.DueDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 延滞ステータスを更新
    /// </summary>
    public async Task<int> UpdateOverdueStatusAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var overdueRentals = await _dbContext.Rentals
            .Where(r => r.Status == RentalStatus.Active && r.DueDate < now)
            .ToListAsync(cancellationToken);

        foreach (var rental in overdueRentals)
        {
            rental.Status = RentalStatus.Overdue;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated {Count} rentals to overdue status", overdueRentals.Count);

        return overdueRentals.Count;
    }
}
