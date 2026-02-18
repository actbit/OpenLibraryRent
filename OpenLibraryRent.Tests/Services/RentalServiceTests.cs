using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OpenLibraryRent.Models;
using OpenLibraryRent.Services;
using Xunit;

namespace OpenLibraryRent.Tests.Services;

public class RentalServiceTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Mock<ILogger<RentalService>> _loggerMock;
    private readonly RentalService _service;
    private readonly string _tenantId = "test-tenant";

    public RentalServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var multiTenantContextAccessor = new MockMultiTenantContextAccessor(_tenantId);
        _dbContext = new ApplicationDbContext(multiTenantContextAccessor, options);
        _loggerMock = new Mock<ILogger<RentalService>>();
        _service = new RentalService(_dbContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task BorrowAsync_Creates_Rental_When_BookCopy_Available()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var book = await CreateTestBookAsync();
        var bookCopy = await CreateTestBookCopyAsync(book.Id);

        // Act
        var rental = await _service.BorrowAsync(user.Id, bookCopy.Id, loanPeriodDays: 14);

        // Assert
        Assert.NotNull(rental);
        Assert.Equal(user.Id, rental.UserId);
        Assert.Equal(bookCopy.Id, rental.BookCopyId);
        Assert.Equal(RentalStatus.Active, rental.Status);
        Assert.True(rental.DueDate > rental.BorrowedAt);
    }

    [Fact]
    public async Task BorrowAsync_Throws_When_BookCopy_Not_Found()
    {
        // Arrange
        var user = await CreateTestUserAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.BorrowAsync(user.Id, Guid.NewGuid(), loanPeriodDays: 14));
    }

    [Fact]
    public async Task BorrowAsync_Throws_When_BookCopy_Not_Available()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var book = await CreateTestBookAsync();
        var bookCopy = await CreateTestBookCopyAsync(book.Id, BookCopyStatus.Borrowed);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.BorrowAsync(user.Id, bookCopy.Id, loanPeriodDays: 14));
    }

    [Fact]
    public async Task BorrowAsync_Updates_BookCopy_Status()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var book = await CreateTestBookAsync();
        var bookCopy = await CreateTestBookCopyAsync(book.Id);

        // Act
        await _service.BorrowAsync(user.Id, bookCopy.Id, loanPeriodDays: 14);

        // Assert
        var updatedCopy = await _dbContext.BookCopies.FindAsync(bookCopy.Id);
        Assert.Equal(BookCopyStatus.Borrowed, updatedCopy!.Status);
    }

    [Fact]
    public async Task BorrowByIsbnAsync_Returns_Null_When_Book_Not_Found()
    {
        // Arrange
        var user = await CreateTestUserAsync();

        // Act
        var result = await _service.BorrowByIsbnAsync(user.Id, "9999999999", loanPeriodDays: 14);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task BorrowByIsbnAsync_Returns_Null_When_No_Available_Copy()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var book = await CreateTestBookAsync(isbn: "1234567890");
        await CreateTestBookCopyAsync(book.Id, BookCopyStatus.Borrowed);

        // Act
        var result = await _service.BorrowByIsbnAsync(user.Id, "1234567890", loanPeriodDays: 14);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task BorrowByIsbnAsync_Creates_Rental_When_Available()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var book = await CreateTestBookAsync(isbn: "1234567890");
        var bookCopy = await CreateTestBookCopyAsync(book.Id);

        // Act
        var rental = await _service.BorrowByIsbnAsync(user.Id, "1234567890", loanPeriodDays: 14);

        // Assert
        Assert.NotNull(rental);
        Assert.Equal(bookCopy.Id, rental.BookCopyId);
    }

    [Fact]
    public async Task ReturnAsync_Creates_History_And_Updates_Rental()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var book = await CreateTestBookAsync();
        var bookCopy = await CreateTestBookCopyAsync(book.Id);
        var rental = await _service.BorrowAsync(user.Id, bookCopy.Id, loanPeriodDays: 14);

        // Act
        var history = await _service.ReturnAsync(rental.Id);

        // Assert
        Assert.NotNull(history);
        Assert.Equal(rental.Id, history.OriginalRentalId);
        Assert.True(history.ReturnedAt >= history.BorrowedAt);
        Assert.Equal(0, history.OverdueDays);

        var updatedRental = await _dbContext.Rentals.FindAsync(rental.Id);
        Assert.Equal(RentalStatus.Returned, updatedRental!.Status);
    }

    [Fact]
    public async Task ReturnAsync_Throws_When_Rental_Not_Found()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ReturnAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ReturnAsync_Throws_When_Rental_Already_Returned()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var book = await CreateTestBookAsync();
        var bookCopy = await CreateTestBookCopyAsync(book.Id);
        var rental = await _service.BorrowAsync(user.Id, bookCopy.Id, loanPeriodDays: 14);
        await _service.ReturnAsync(rental.Id);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ReturnAsync(rental.Id));
    }

    [Fact]
    public async Task ReturnAsync_Updates_BookCopy_Status_To_Available()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var book = await CreateTestBookAsync();
        var bookCopy = await CreateTestBookCopyAsync(book.Id);
        var rental = await _service.BorrowAsync(user.Id, bookCopy.Id, loanPeriodDays: 14);

        // Act
        await _service.ReturnAsync(rental.Id);

        // Assert
        var updatedCopy = await _dbContext.BookCopies.FindAsync(bookCopy.Id);
        Assert.Equal(BookCopyStatus.Available, updatedCopy!.Status);
    }

    [Fact]
    public async Task ReturnByIsbnAsync_Returns_Null_When_Book_Not_Found()
    {
        // Arrange
        var user = await CreateTestUserAsync();

        // Act
        var result = await _service.ReturnByIsbnAsync("9999999999", user.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ReturnByIsbnAsync_Returns_Null_When_No_Active_Rental()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var book = await CreateTestBookAsync(isbn: "1234567890");
        await CreateTestBookCopyAsync(book.Id);

        // Act
        var result = await _service.ReturnByIsbnAsync("1234567890", user.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetOverdueRentalsAsync_Returns_Only_Overdue_Rentals()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var book1 = await CreateTestBookAsync(isbn: "1111111111");
        var book2 = await CreateTestBookAsync(isbn: "2222222222");
        var copy1 = await CreateTestBookCopyAsync(book1.Id);
        var copy2 = await CreateTestBookCopyAsync(book2.Id);

        // Create overdue rental (loan period 0 days means due immediately)
        var overdueRental = await _service.BorrowAsync(user.Id, copy1.Id, loanPeriodDays: 0);
        // Manually set due date to past
        overdueRental.DueDate = DateTime.UtcNow.AddDays(-5);
        await _dbContext.SaveChangesAsync();

        // Create normal rental
        var normalRental = await _service.BorrowAsync(user.Id, copy2.Id, loanPeriodDays: 14);

        // Act
        var overdueRentals = await _service.GetOverdueRentalsAsync();

        // Assert
        Assert.Single(overdueRentals);
        Assert.Equal(overdueRental.Id, overdueRentals[0].Id);
    }

    [Fact]
    public async Task UpdateOverdueStatusAsync_Updates_Status_To_Overdue()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var book = await CreateTestBookAsync();
        var copy = await CreateTestBookCopyAsync(book.Id);

        var rental = await _service.BorrowAsync(user.Id, copy.Id, loanPeriodDays: 0);
        rental.DueDate = DateTime.UtcNow.AddDays(-1);
        await _dbContext.SaveChangesAsync();

        // Act
        var count = await _service.UpdateOverdueStatusAsync();

        // Assert
        Assert.Equal(1, count);

        var updatedRental = await _dbContext.Rentals.FindAsync(rental.Id);
        Assert.Equal(RentalStatus.Overdue, updatedRental!.Status);
    }

    #region Helper Methods

    private async Task<ApplicationUser> CreateTestUserAsync()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"user_{Guid.NewGuid():N}",
            Email = $"test_{Guid.NewGuid():N}@example.com",
            TenantId = _tenantId
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    private async Task<Book> CreateTestBookAsync(string? isbn = null)
    {
        var book = new Book
        {
            Id = Guid.NewGuid(),
            Isbn = isbn ?? $"isbn_{Guid.NewGuid():N}",
            Title = $"Test Book {Guid.NewGuid():N}",
            TenantId = _tenantId,
            TotalCopies = 1,
            AvailableCopies = 1
        };

        _dbContext.Books.Add(book);
        await _dbContext.SaveChangesAsync();
        return book;
    }

    private async Task<BookCopy> CreateTestBookCopyAsync(Guid bookId, BookCopyStatus status = BookCopyStatus.Available)
    {
        var copy = new BookCopy
        {
            Id = Guid.NewGuid(),
            BookId = bookId,
            InventoryCode = $"INV-{Guid.NewGuid():N}",
            Status = status,
            TenantId = _tenantId
        };

        _dbContext.BookCopies.Add(copy);
        await _dbContext.SaveChangesAsync();
        return copy;
    }

    #endregion
}

/// <summary>
/// Mock implementation of IMultiTenantContextAccessor for testing
/// </summary>
internal class MockMultiTenantContextAccessor : Finbuckle.MultiTenant.Abstractions.IMultiTenantContextAccessor
{
    public Finbuckle.MultiTenant.Abstractions.IMultiTenantContext MultiTenantContext { get; set; }

    public MockMultiTenantContextAccessor(string tenantId)
    {
        var tenantInfo = new ApplicationTenantInfo
        {
            Id = tenantId,
            Identifier = tenantId,
            Name = $"Tenant {tenantId}"
        };

        MultiTenantContext = new Finbuckle.MultiTenant.MultiTenantContext<ApplicationTenantInfo>
        {
            TenantInfo = tenantInfo
        };
    }

    public void SetTenantContext(Finbuckle.MultiTenant.Abstractions.IMultiTenantContext context)
    {
        MultiTenantContext = context;
    }
}
