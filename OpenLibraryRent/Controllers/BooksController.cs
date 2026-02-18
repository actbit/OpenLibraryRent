using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenLibraryRent.Dtos;
using OpenLibraryRent.Models;
using OpenLibraryRent.Services;

namespace OpenLibraryRent.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class BooksController : BaseController
{
    private readonly ApplicationDbContext _dbContext;
    private readonly OpenLibraryService _openLibraryService;
    private readonly ILogger<BooksController> _logger;

    public BooksController(
        ApplicationDbContext dbContext,
        OpenLibraryService openLibraryService,
        ILogger<BooksController> logger)
    {
        _dbContext = dbContext;
        _openLibraryService = openLibraryService;
        _logger = logger;
    }

    /// <summary>
    /// 書籍一覧を取得
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<BookListResponse>> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _dbContext.Books
            .Include(b => b.Copies)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(b =>
                b.Title.Contains(search) ||
                (b.Authors != null && b.Authors.Contains(search)) ||
                b.Isbn.Contains(search));
        }

        var total = await query.CountAsync();
        var books = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BookListItemDto
            {
                Id = b.Id,
                Isbn = b.Isbn,
                Title = b.Title,
                Authors = b.Authors,
                Publisher = b.Publisher,
                PublishYear = b.PublishYear,
                CoverImageUrl = b.CoverImageUrl,
                TotalCopies = b.TotalCopies,
                AvailableCopies = b.AvailableCopies
            })
            .ToListAsync();

        return Ok(new BookListResponse
        {
            Books = books,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// 書籍詳細を取得
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<BookDetailDto>> Get(Guid id)
    {
        var book = await _dbContext.Books
            .Include(b => b.Copies)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (book == null)
        {
            return NotFound(new MessageResponse("Book not found"));
        }

        return Ok(new BookDetailDto
        {
            Id = book.Id,
            Isbn = book.Isbn,
            Title = book.Title,
            Authors = book.Authors,
            Publisher = book.Publisher,
            PublishYear = book.PublishYear,
            PageCount = book.PageCount,
            CoverImageUrl = book.CoverImageUrl,
            Description = book.Description,
            TotalCopies = book.TotalCopies,
            AvailableCopies = book.AvailableCopies,
            Copies = book.Copies?.Select(c => new BookCopyDto
            {
                Id = c.Id,
                InventoryCode = c.InventoryCode,
                Status = c.Status.ToString(),
                Notes = c.Notes
            }).ToList()
        });
    }

    /// <summary>
    /// Open LibraryからISBNで書籍情報を取得
    /// </summary>
    [HttpGet("fetch-from-openlibrary/{isbn}")]
    [AllowAnonymous]
    public async Task<ActionResult<OpenLibraryBookDto>> FetchFromOpenLibrary(string isbn)
    {
        var bookData = await _openLibraryService.GetBookByIsbnAsync(isbn);

        if (bookData == null)
        {
            return NotFound(new MessageResponse("Book not found in Open Library"));
        }

        return Ok(new OpenLibraryBookDto
        {
            Isbn = bookData.Isbn,
            Title = bookData.Title,
            Authors = bookData.Authors,
            Publisher = bookData.Publisher,
            PublishYear = bookData.PublishYear,
            PageCount = bookData.PageCount,
            CoverImageUrl = bookData.CoverImageUrl,
            Description = bookData.Description
        });
    }

    /// <summary>
    /// 書籍を登録
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BookCreateResultDto>> Create([FromBody] CreateBookRequest request)
    {
        // 既存のISBNチェック
        var existing = await _dbContext.Books
            .FirstOrDefaultAsync(b => b.Isbn == request.Isbn);

        if (existing != null)
        {
            return Conflict(new BookConflictDto { Message = "Book with this ISBN already exists", BookId = existing.Id });
        }

        var book = new Book
        {
            Isbn = request.Isbn,
            Title = request.Title,
            Authors = request.Authors,
            Publisher = request.Publisher,
            PublishYear = request.PublishYear,
            PageCount = request.PageCount,
            CoverImageUrl = request.CoverImageUrl,
            Description = request.Description,
            TotalCopies = 0,
            AvailableCopies = 0
        };

        _dbContext.Books.Add(book);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Book created: {Id} - {Title}", book.Id, book.Title);

        return CreatedAtAction(nameof(Get), new { id = book.Id }, new BookCreateResultDto
        {
            Id = book.Id,
            Isbn = book.Isbn,
            Title = book.Title
        });
    }

    /// <summary>
    /// Open Libraryから書籍情報を取得して登録
    /// </summary>
    [HttpPost("register-from-openlibrary/{isbn}")]
    public async Task<ActionResult<BookCreateResultDto>> RegisterFromOpenLibrary(string isbn)
    {
        // 既存のISBNチェック
        var existing = await _dbContext.Books
            .FirstOrDefaultAsync(b => b.Isbn == isbn);

        if (existing != null)
        {
            return Conflict(new BookConflictDto { Message = "Book with this ISBN already exists", BookId = existing.Id });
        }

        var bookData = await _openLibraryService.GetBookByIsbnAsync(isbn);

        if (bookData == null)
        {
            return NotFound(new MessageResponse("Book not found in Open Library"));
        }

        var book = new Book
        {
            Isbn = bookData.Isbn,
            Title = bookData.Title,
            Authors = bookData.Authors,
            Publisher = bookData.Publisher,
            PublishYear = bookData.PublishYear,
            PageCount = bookData.PageCount,
            CoverImageUrl = bookData.CoverImageUrl,
            Description = bookData.Description,
            TotalCopies = 0,
            AvailableCopies = 0
        };

        _dbContext.Books.Add(book);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Book registered from Open Library: {Id} - {Title}", book.Id, book.Title);

        return CreatedAtAction(nameof(Get), new { id = book.Id }, new BookCreateResultDto
        {
            Id = book.Id,
            Isbn = book.Isbn,
            Title = book.Title
        });
    }

    /// <summary>
    /// 書籍を更新
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<MessageResponse>> Update(Guid id, [FromBody] UpdateBookRequest request)
    {
        var book = await _dbContext.Books.FindAsync(id);

        if (book == null)
        {
            return NotFound(new MessageResponse("Book not found"));
        }

        if (!string.IsNullOrEmpty(request.Title))
            book.Title = request.Title;

        if (request.Authors != null)
            book.Authors = request.Authors;

        if (request.Publisher != null)
            book.Publisher = request.Publisher;

        if (request.PublishYear.HasValue)
            book.PublishYear = request.PublishYear;

        if (request.PageCount.HasValue)
            book.PageCount = request.PageCount;

        if (request.CoverImageUrl != null)
            book.CoverImageUrl = request.CoverImageUrl;

        if (request.Description != null)
            book.Description = request.Description;

        book.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(new MessageResponse("Book updated successfully"));
    }

    /// <summary>
    /// 書籍を削除
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<MessageResponse>> Delete(Guid id)
    {
        var book = await _dbContext.Books
            .Include(b => b.Copies)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (book == null)
        {
            return NotFound(new MessageResponse("Book not found"));
        }

        if (book.Copies?.Any() == true)
        {
            return BadRequest(new MessageResponse("Cannot delete book with copies"));
        }

        _dbContext.Books.Remove(book);
        await _dbContext.SaveChangesAsync();

        return Ok(new MessageResponse("Book deleted successfully"));
    }
}

public class CreateBookRequest
{
    public string Isbn { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Authors { get; set; }
    public string? Publisher { get; set; }
    public int? PublishYear { get; set; }
    public int? PageCount { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Description { get; set; }
}

public class UpdateBookRequest
{
    public string? Title { get; set; }
    public string? Authors { get; set; }
    public string? Publisher { get; set; }
    public int? PublishYear { get; set; }
    public int? PageCount { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Description { get; set; }
}
