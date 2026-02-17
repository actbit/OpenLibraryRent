using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenLibraryRent.Models;
using OpenLibraryRent.Services;

namespace OpenLibraryRent.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class RentalsController : BaseController
{
    private readonly ApplicationDbContext _dbContext;
    private readonly RentalService _rentalService;
    private readonly ILogger<RentalsController> _logger;

    public RentalsController(
        ApplicationDbContext dbContext,
        RentalService rentalService,
        ILogger<RentalsController> logger)
    {
        _dbContext = dbContext;
        _rentalService = rentalService;
        _logger = logger;
    }

    /// <summary>
    /// 現在のユーザーの貸出一覧を取得
    /// </summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMyRentals()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var rentals = await _dbContext.Rentals
            .Include(r => r.Book)
            .Include(r => r.BookCopy)
            .Where(r => r.UserId == userId && (r.Status == RentalStatus.Active || r.Status == RentalStatus.Overdue))
            .Select(r => new
            {
                r.Id,
                Book = new
                {
                    r.Book!.Id,
                    r.Book.Isbn,
                    r.Book.Title,
                    r.Book.CoverImageUrl
                },
                BookCopy = new
                {
                    r.BookCopy!.Id,
                    r.BookCopy.InventoryCode
                },
                r.BorrowedAt,
                r.DueDate,
                r.Status,
                OverdueDays = r.IsOverdue ? (int)(DateTime.UtcNow - r.DueDate).TotalDays : 0
            })
            .OrderBy(r => r.DueDate)
            .ToListAsync();

        return Ok(rentals);
    }

    /// <summary>
    /// すべてのアクティブな貸出一覧を取得（管理者用）
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> List([FromQuery] bool? overdueOnly = false)
    {
        var query = _dbContext.Rentals
            .Include(r => r.Book)
            .Include(r => r.BookCopy)
            .Include(r => r.User)
            .Where(r => r.Status == RentalStatus.Active || r.Status == RentalStatus.Overdue);

        if (overdueOnly == true)
        {
            query = query.Where(r => r.DueDate < DateTime.UtcNow);
        }

        var rentals = await query
            .Select(r => new
            {
                r.Id,
                Book = new
                {
                    r.Book!.Id,
                    r.Book.Isbn,
                    r.Book.Title,
                    r.Book.CoverImageUrl
                },
                BookCopy = new
                {
                    r.BookCopy!.Id,
                    r.BookCopy.InventoryCode
                },
                User = new
                {
                    r.User!.Id,
                    DisplayName = r.User.DisplayName ?? r.User.UserName
                },
                r.BorrowedAt,
                r.DueDate,
                r.Status,
                OverdueDays = r.IsOverdue ? (int)(DateTime.UtcNow - r.DueDate).TotalDays : 0
            })
            .OrderBy(r => r.DueDate)
            .ToListAsync();

        return Ok(rentals);
    }

    /// <summary>
    /// 延滞一覧を取得
    /// </summary>
    [HttpGet("overdue")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> GetOverdue()
    {
        var rentals = await _rentalService.GetOverdueRentalsAsync();

        var result = rentals.Select(r => new
        {
            r.Id,
            Book = new
            {
                r.Book!.Id,
                r.Book.Isbn,
                r.Book.Title,
                r.Book.CoverImageUrl
            },
            BookCopy = new
            {
                r.BookCopy!.Id,
                r.BookCopy.InventoryCode
            },
            User = new
            {
                r.User!.Id,
                DisplayName = r.User.DisplayName ?? r.User.UserName,
                r.User.Email
            },
            r.BorrowedAt,
            r.DueDate,
            OverdueDays = (int)(DateTime.UtcNow - r.DueDate).TotalDays
        });

        return Ok(result);
    }

    /// <summary>
    /// 貸出処理
    /// </summary>
    [HttpPost("borrow")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> Borrow([FromBody] BorrowRequest request)
    {
        var userId = request.UserId ?? GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        // テナントの貸出設定を取得
        var tenantIdentifier = HttpContext.GetRouteValue("tenant")?.ToString();
        var tenant = await _dbContext.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == tenantIdentifier);

        var loanPeriodDays = tenant?.Detail?.LoanPeriodDays ?? 14;
        var maxLoans = tenant?.Detail?.MaxLoansPerUser ?? 5;

        // 現在の貸出数をチェック
        var currentCount = await _dbContext.Rentals
            .CountAsync(r => r.UserId == userId && r.Status == RentalStatus.Active);

        if (currentCount >= maxLoans)
        {
            return BadRequest(new { message = $"Maximum loans ({maxLoans}) reached" });
        }

        try
        {
            Rental rental;

            if (request.BookCopyId.HasValue)
            {
                rental = await _rentalService.BorrowAsync(
                    userId.Value,
                    request.BookCopyId.Value,
                    loanPeriodDays,
                    request.Notes);
            }
            else if (!string.IsNullOrEmpty(request.Isbn))
            {
                rental = (await _rentalService.BorrowByIsbnAsync(
                    userId.Value,
                    request.Isbn,
                    loanPeriodDays,
                    request.Notes))!;

                if (rental == null)
                {
                    return NotFound(new { message = "No available copy found for this ISBN" });
                }
            }
            else
            {
                return BadRequest(new { message = "BookCopyId or Isbn is required" });
            }

            _logger.LogInformation("Book borrowed: RentalId={RentalId}, UserId={UserId}", rental.Id, userId);

            return Ok(new
            {
                rental.Id,
                rental.BookId,
                rental.BookCopyId,
                rental.UserId,
                rental.BorrowedAt,
                rental.DueDate
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to borrow book");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 返却処理
    /// </summary>
    [HttpPost("{id}/return")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> Return(Guid id, [FromBody] ReturnRequest? request = null)
    {
        try
        {
            var history = await _rentalService.ReturnAsync(id, request?.Notes);

            _logger.LogInformation("Book returned: RentalId={RentalId}, OverdueDays={OverdueDays}",
                id, history.OverdueDays);

            return Ok(new
            {
                history.Id,
                history.BookId,
                history.BookCopyId,
                history.UserId,
                history.BorrowedAt,
                history.DueDate,
                history.ReturnedAt,
                history.OverdueDays
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to return book");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// ISBNで返却処理
    /// </summary>
    [HttpPost("return-by-isbn")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> ReturnByIsbn([FromBody] ReturnByIsbnRequest request)
    {
        var userId = request.UserId ?? GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var history = await _rentalService.ReturnByIsbnAsync(request.Isbn, userId.Value, request.Notes);

            if (history == null)
            {
                return NotFound(new { message = "No active rental found for this ISBN and user" });
            }

            _logger.LogInformation("Book returned by ISBN: Isbn={Isbn}, UserId={UserId}",
                request.Isbn, userId);

            return Ok(new
            {
                history.Id,
                history.BookId,
                history.BookCopyId,
                history.UserId,
                history.BorrowedAt,
                history.DueDate,
                history.ReturnedAt,
                history.OverdueDays
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to return book by ISBN");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 自分で返却処理
    /// </summary>
    [HttpPost("my/{id}/return")]
    public async Task<IActionResult> ReturnMy(Guid id, [FromBody] ReturnRequest? request = null)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var rental = await _dbContext.Rentals
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

        if (rental == null)
        {
            return NotFound(new { message = "Rental not found" });
        }

        try
        {
            var history = await _rentalService.ReturnAsync(id, request?.Notes);

            _logger.LogInformation("User returned book: RentalId={RentalId}, UserId={UserId}",
                id, userId);

            return Ok(new
            {
                history.Id,
                history.BookId,
                history.ReturnedAt,
                history.OverdueDays
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to return book");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 貸出履歴を取得
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var query = _dbContext.RentalHistories
            .Include(h => h.Book)
            .Include(h => h.BookCopy)
            .Where(h => h.UserId == userId);

        var total = await query.CountAsync();
        var history = await query
            .OrderByDescending(h => h.ReturnedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new
            {
                h.Id,
                Book = new
                {
                    h.Book!.Id,
                    h.Book.Isbn,
                    h.Book.Title,
                    h.Book.CoverImageUrl
                },
                h.BorrowedAt,
                h.DueDate,
                h.ReturnedAt,
                h.OverdueDays
            })
            .ToListAsync();

        return Ok(new { history, total, page, pageSize });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }
}

public class BorrowRequest
{
    public Guid? BookCopyId { get; set; }
    public string? Isbn { get; set; }
    public Guid? UserId { get; set; }
    public string? Notes { get; set; }
}

public class ReturnRequest
{
    public string? Notes { get; set; }
}

public class ReturnByIsbnRequest
{
    public string Isbn { get; set; } = null!;
    public Guid? UserId { get; set; }
    public string? Notes { get; set; }
}
