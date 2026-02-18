using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using OpenLibraryRent.Services.Caching;

namespace OpenLibraryRent.Tests.Services.Caching;

public class MemoryCacheServiceTests : IDisposable
{
    private readonly MemoryCache _memoryCache;
    private readonly Mock<ILogger<MemoryCacheService>> _loggerMock;
    private readonly MemoryCacheService _cacheService;

    public MemoryCacheServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<MemoryCacheService>>();
        _cacheService = new MemoryCacheService(_memoryCache, _loggerMock.Object);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    [Fact]
    public async Task SetAsync_And_GetAsync_Returns_Cached_Value()
    {
        // Arrange
        var key = "test-key";
        var value = new TestObject { Id = 1, Name = "Test" };

        // Act
        await _cacheService.SetAsync(key, value);
        var result = await _cacheService.GetAsync<TestObject>(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Test", result.Name);
    }

    [Fact]
    public async Task GetAsync_Returns_Null_For_NonExistent_Key()
    {
        // Arrange
        var key = "non-existent-key";

        // Act
        var result = await _cacheService.GetAsync<TestObject>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_Removes_Cached_Value()
    {
        // Arrange
        var key = "test-key";
        var value = new TestObject { Id = 1, Name = "Test" };
        await _cacheService.SetAsync(key, value);

        // Act
        await _cacheService.RemoveAsync(key);
        var result = await _cacheService.GetAsync<TestObject>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ExistsAsync_Returns_True_For_Cached_Key()
    {
        // Arrange
        var key = "test-key";
        var value = new TestObject { Id = 1, Name = "Test" };
        await _cacheService.SetAsync(key, value);

        // Act
        var exists = await _cacheService.ExistsAsync(key);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_Returns_False_For_NonExistent_Key()
    {
        // Arrange
        var key = "non-existent-key";

        // Act
        var exists = await _cacheService.ExistsAsync(key);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task GetOrCreateAsync_Returns_Cached_Value_When_Exists()
    {
        // Arrange
        var key = "test-key";
        var cachedValue = new TestObject { Id = 1, Name = "Cached" };
        await _cacheService.SetAsync(key, cachedValue);

        var factoryCalled = false;

        // Act
        var result = await _cacheService.GetOrCreateAsync(
            key,
            _ =>
            {
                factoryCalled = true;
                return Task.FromResult(new TestObject { Id = 2, Name = "New" });
            });

        // Assert
        Assert.False(factoryCalled);
        Assert.Equal(1, result.Id);
        Assert.Equal("Cached", result.Name);
    }

    [Fact]
    public async Task GetOrCreateAsync_Calls_Factory_And_Caches_When_Not_Exists()
    {
        // Arrange
        var key = "test-key";
        var factoryCalled = false;

        // Act
        var result = await _cacheService.GetOrCreateAsync(
            key,
            _ =>
            {
                factoryCalled = true;
                return Task.FromResult(new TestObject { Id = 2, Name = "New" });
            });

        // Assert
        Assert.True(factoryCalled);
        Assert.Equal(2, result.Id);
        Assert.Equal("New", result.Name);

        // Verify it's cached
        var cachedResult = await _cacheService.GetAsync<TestObject>(key);
        Assert.NotNull(cachedResult);
        Assert.Equal(2, cachedResult.Id);
    }

    [Fact]
    public async Task SetAsync_With_Expiration_Expires_After_Timeout()
    {
        // Arrange
        var key = "test-key";
        var value = new TestObject { Id = 1, Name = "Test" };
        var expiration = TimeSpan.FromMilliseconds(100);

        // Act
        await _cacheService.SetAsync(key, value, expiration);

        // Assert - immediately available
        var immediateResult = await _cacheService.GetAsync<TestObject>(key);
        Assert.NotNull(immediateResult);

        // Wait for expiration
        await Task.Delay(200);

        // Assert - expired
        var expiredResult = await _cacheService.GetAsync<TestObject>(key);
        Assert.Null(expiredResult);
    }

    [Fact]
    public async Task RemoveByPatternAsync_Removes_Matching_Keys()
    {
        // Arrange
        await _cacheService.SetAsync("tenant:1:info", new TestObject { Id = 1 });
        await _cacheService.SetAsync("tenant:1:settings", new TestObject { Id = 2 });
        await _cacheService.SetAsync("tenant:2:info", new TestObject { Id = 3 });
        await _cacheService.SetAsync("book:isbn:123", new TestObject { Id = 4 });

        // Act
        await _cacheService.RemoveByPatternAsync("tenant:1:*");

        // Assert
        Assert.Null(await _cacheService.GetAsync<TestObject>("tenant:1:info"));
        Assert.Null(await _cacheService.GetAsync<TestObject>("tenant:1:settings"));
        Assert.NotNull(await _cacheService.GetAsync<TestObject>("tenant:2:info"));
        Assert.NotNull(await _cacheService.GetAsync<TestObject>("book:isbn:123"));
    }

    [Fact]
    public async Task Cache_Works_With_Different_Types()
    {
        // Arrange & Act - String
        await _cacheService.SetAsync("string-key", "test value");
        var stringResult = await _cacheService.GetAsync<string>("string-key");
        Assert.Equal("test value", stringResult);

        // Arrange & Act - Int
        await _cacheService.SetAsync("int-key", 42);
        var intResult = await _cacheService.GetAsync<int>("int-key");
        Assert.Equal(42, intResult);

        // Arrange & Act - List
        await _cacheService.SetAsync("list-key", new List<int> { 1, 2, 3 });
        var listResult = await _cacheService.GetAsync<List<int>>("list-key");
        Assert.NotNull(listResult);
        Assert.Equal(3, listResult.Count);
    }

    private class TestObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
