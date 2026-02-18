using OpenLibraryRent.Services.Caching;
using Xunit;

namespace OpenLibraryRent.Tests.Services.Caching;

public class CacheKeysTests
{
    [Fact]
    public void TenantInfo_Returns_Correct_Format()
    {
        // Arrange
        var tenantId = "tenant-123";

        // Act
        var key = CacheKeys.TenantInfo(tenantId);

        // Assert
        Assert.Equal("OpenLibraryRent:tenant:tenant-123:info", key);
    }

    [Fact]
    public void TenantSettings_Returns_Correct_Format()
    {
        // Arrange
        var tenantId = "tenant-456";

        // Act
        var key = CacheKeys.TenantSettings(tenantId);

        // Assert
        Assert.Equal("OpenLibraryRent:tenant:tenant-456:settings", key);
    }

    [Fact]
    public void BookByIsbn_Returns_Correct_Format()
    {
        // Arrange
        var isbn = "978-4-123456-78-9";

        // Act
        var key = CacheKeys.BookByIsbn(isbn);

        // Assert
        Assert.Equal("OpenLibraryRent:book:isbn:978-4-123456-78-9", key);
    }

    [Fact]
    public void UserPermissions_Returns_Correct_Format()
    {
        // Arrange
        var tenantId = "tenant-123";
        var userId = "user-456";

        // Act
        var key = CacheKeys.UserPermissions(tenantId, userId);

        // Assert
        Assert.Equal("OpenLibraryRent:tenant:tenant-123:user:user-456:permissions", key);
    }

    [Fact]
    public void TenantAll_Returns_Correct_Pattern_Format()
    {
        // Arrange
        var tenantId = "tenant-123";

        // Act
        var pattern = CacheKeys.TenantAll(tenantId);

        // Assert
        Assert.Equal("OpenLibraryRent:tenant:tenant-123:*", pattern);
    }

    [Theory]
    [InlineData("tenant-1")]
    [InlineData("abc123")]
    [InlineData("test-tenant-id")]
    public void TenantInfo_Consistent_For_Same_TenantId(string tenantId)
    {
        // Act
        var key1 = CacheKeys.TenantInfo(tenantId);
        var key2 = CacheKeys.TenantInfo(tenantId);

        // Assert
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void CacheKeys_Are_Deterministic()
    {
        // Arrange
        var tenantId = "test";
        var isbn = "1234567890";
        var userId = "user1";

        // Act - Call multiple times
        var tenantKey1 = CacheKeys.TenantInfo(tenantId);
        var tenantKey2 = CacheKeys.TenantInfo(tenantId);
        var bookKey1 = CacheKeys.BookByIsbn(isbn);
        var bookKey2 = CacheKeys.BookByIsbn(isbn);
        var permKey1 = CacheKeys.UserPermissions(tenantId, userId);
        var permKey2 = CacheKeys.UserPermissions(tenantId, userId);

        // Assert - Same inputs produce same keys
        Assert.Equal(tenantKey1, tenantKey2);
        Assert.Equal(bookKey1, bookKey2);
        Assert.Equal(permKey1, permKey2);
    }

    [Fact]
    public void Different_Inputs_Produce_Different_Keys()
    {
        // Arrange & Act
        var key1 = CacheKeys.TenantInfo("tenant-1");
        var key2 = CacheKeys.TenantInfo("tenant-2");

        // Assert
        Assert.NotEqual(key1, key2);
    }
}
