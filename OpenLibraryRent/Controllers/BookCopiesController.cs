using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenLibraryRent.Models;

namespace OpenLibraryRent.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class BookCopiesController : BaseController
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<BookCopiesController> _logger;

    public BookCopiesController(
        ApplicationDbContext dbContext,
        ILogger<BookCopiesController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// 書籍の個体一覧を取得
    /// </summary>
    [HttpGet("book/{bookId}")]
    public async Task<IActionResult> ListByBook(Guid bookId)
    {
        var copies = await _dbContext.BookCopies
            .Include(c => c.CurrentRental)
                .ThenInclude(r => r!.User)
            .Where(c => c.BookId == bookId)
            .Select(c => new
            {
                c.Id,
                c.InventoryCode,
                c.Status,
                c.Notes,
                CurrentRental = c.CurrentRental != null ? new
                {
                    c.CurrentRental.Id,
                    c.CurrentRental.UserId,
                    UserName = c.CurrentRental.User!.DisplayName ?? c.CurrentRental.User!.UserName,
                    c.CurrentRental.DueDate,
                    c.CurrentRental.BorrowedAt
                } : null
            })
            .ToListAsync();

        return Ok(copies);
    }

    /// <summary>
    /// 書籍個体を追加
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookCopyRequest request)
    {
        var book = await _dbContext.Books.FindAsync(request.BookId);

        if (book == null)
        {
            return NotFound(new { message = "Book not found" });
        }

        // 管理番号の重複チェック
        var existingCode = await _dbContext.BookCopies
            .FirstOrDefaultAsync(c => c.InventoryCode == request.InventoryCode);

        if (existingCode != null)
        {
            return Conflict(new { message = "Inventory code already exists" });
        }

        var copy = new BookCopy
        {
            BookId = request.BookId,
            InventoryCode = request.InventoryCode,
            Status = BookCopyStatus.Available,
            Notes = request.Notes
        };

        _dbContext.BookCopies.Add(copy);

        // 書籍の冊数を更新
        book.TotalCopies++;
        book.AvailableCopies++;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Book copy created: {Id} - {InventoryCode}", copy.Id, copy.InventoryCode);

        return CreatedAtAction(nameof(Get), new { id = copy.Id }, new { copy.Id, copy.InventoryCode, copy.Status });
    }

    /// <summary>
    /// 書籍個体詳細を取得
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var copy = await _dbContext.BookCopies
            .Include(c => c.Book)
            .Include(c => c.CurrentRental)
                .ThenInclude(r => r!.User)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (copy == null)
        {
            return NotFound(new { message = "Book copy not found" });
        }

        return Ok(new
        {
            copy.Id,
            copy.InventoryCode,
            copy.Status,
            copy.Notes,
            Book = new
            {
                copy.Book!.Id,
                copy.Book.Isbn,
                copy.Book.Title
            },
            CurrentRental = copy.CurrentRental != null ? new
            {
                copy.CurrentRental.Id,
                copy.CurrentRental.UserId,
                UserName = copy.CurrentRental.User!.DisplayName ?? copy.CurrentRental.User!.UserName,
                copy.CurrentRental.DueDate,
                copy.CurrentRental.BorrowedAt
            } : null
        });
    }

    /// <summary>
    /// 書籍個体の状態を更新
    /// </summary>
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateBookCopyStatusRequest request)
    {
        var copy = await _dbContext.BookCopies
            .Include(c => c.Book)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (copy == null)
        {
            return NotFound(new { message = "Book copy not found" });
        }

        var oldStatus = copy.Status;
        copy.Status = request.Status;
        copy.Notes = request.Notes ?? copy.Notes;
        copy.UpdatedAt = DateTime.UtcNow;

        // 冊数を更新
        if (oldStatus == BookCopyStatus.Available && request.Status != BookCopyStatus.Available)
        {
            copy.Book!.AvailableCopies--;
        }
        else if (oldStatus != BookCopyStatus.Available && request.Status == BookCopyStatus.Available)
        {
            copy.Book!.AvailableCopies++;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Book copy status updated: {Id} - {Status}", copy.Id, copy.Status);

        return Ok(new { message = "Status updated successfully" });
    }

    /// <summary>
    /// 書籍個体を削除
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var copy = await _dbContext.BookCopies
            .Include(c => c.Book)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (copy == null)
        {
            return NotFound(new { message = "Book copy not found" });
        }

        if (copy.Status == BookCopyStatus.Borrowed)
        {
            return BadRequest(new { message = "Cannot delete a borrowed copy" });
        }

        // 冊数を更新
        copy.Book!.TotalCopies--;
        if (copy.Status == BookCopyStatus.Available)
        {
            copy.Book.AvailableCopies--;
        }

        _dbContext.BookCopies.Remove(copy);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Book copy deleted successfully" });
    }
}

public class CreateBookCopyRequest
{
    public Guid BookId { get; set; }
    public string InventoryCode { get; set; } = null!;
    public string? Notes { get; set; }
}

public class UpdateBookCopyStatusRequest
{
    public BookCopyStatus Status { get; set; }
    public string? Notes { get; set; }
}
