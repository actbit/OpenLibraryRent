using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenLibraryRent.Services.Caching;
using Xunit;

namespace OpenLibraryRent.Tests.Services.Caching;

public class CacheServiceExtensionsTests
{
    [Fact]
    public void AddCacheService_Without_Redis_Uses_MemoryCache()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:redis"] = null
            })
            .Build();

        // Act
        services.AddCacheService(configuration);
        var serviceProvider = services.BuildServiceProvider();
        var cacheService = serviceProvider.GetService<ICacheService>();

        // Assert
        Assert.NotNull(cacheService);
        Assert.IsType<MemoryCacheService>(cacheService);
    }

    [Fact]
    public void AddCacheService_With_Redis_Uses_RedisCache()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:redis"] = "localhost:6379"
            })
            .Build();

        // Act
        services.AddCacheService(configuration);
        var serviceProvider = services.BuildServiceProvider();
        var cacheService = serviceProvider.GetService<ICacheService>();

        // Assert
        Assert.NotNull(cacheService);
        Assert.IsType<RedisCacheService>(cacheService);
    }

    [Fact]
    public void AddMemoryCacheService_Registers_MemoryCacheService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddMemoryCacheService();
        var serviceProvider = services.BuildServiceProvider();
        var cacheService = serviceProvider.GetService<ICacheService>();

        // Assert
        Assert.NotNull(cacheService);
        Assert.IsType<MemoryCacheService>(cacheService);
    }

    [Fact]
    public void AddRedisCacheService_Registers_RedisCacheService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = "localhost:6379";
        });

        // Act
        services.AddRedisCacheService();
        var serviceProvider = services.BuildServiceProvider();
        var cacheService = serviceProvider.GetService<ICacheService>();

        // Assert
        Assert.NotNull(cacheService);
        Assert.IsType<RedisCacheService>(cacheService);
    }

    [Fact]
    public void AddCacheService_Registers_As_Singleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        services.AddCacheService(configuration);
        var serviceProvider = services.BuildServiceProvider();
        var cacheService1 = serviceProvider.GetService<ICacheService>();
        var cacheService2 = serviceProvider.GetService<ICacheService>();

        // Assert
        Assert.Same(cacheService1, cacheService2);
    }

    [Fact]
    public async Task Cached_Value_Persists_Across_Service_Resolution()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().Build();
        services.AddCacheService(configuration);
        var serviceProvider = services.BuildServiceProvider();

        var cacheService1 = serviceProvider.GetRequiredService<ICacheService>();
        var cacheService2 = serviceProvider.GetRequiredService<ICacheService>();

        // Act
        await cacheService1.SetAsync("test-key", "test-value");
        var value = await cacheService2.GetAsync<string>("test-key");

        // Assert
        Assert.Equal("test-value", value);
    }
}
