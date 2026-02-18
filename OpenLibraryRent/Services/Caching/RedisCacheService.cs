using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace OpenLibraryRent.Services.Caching;

/// <summary>
/// Redisキャッシュの実装
/// 設定時に有効化されるオプションのキャッシュサービス
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var bytes = await _cache.GetAsync(key, cancellationToken);

        if (bytes == null || bytes.Length == 0)
        {
            return default;
        }

        try
        {
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize cache value for key: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        var options = new DistributedCacheEntryOptions();

        if (expiration.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = expiration;
        }
        else
        {
            options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30); // デフォルト30分
        }

        await _cache.SetAsync(key, bytes, options, cancellationToken);

        _logger.LogDebug("Redis cache set: {Key}, Expiration: {Expiration}", key, expiration);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(key, cancellationToken);

        _logger.LogDebug("Redis cache removed: {Key}", key);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var value = await _cache.GetAsync(key, cancellationToken);
        return value != null && value.Length > 0;
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // IDistributedCacheインターフェースはパターン削除をサポートしていない
        // Redis直接使用時はIServer.Keys()を使用可能だが、
        // ここでは警告ログのみ出力
        _logger.LogWarning("RemoveByPatternAsync is not fully supported with IDistributedCache. Pattern: {Pattern}", pattern);

        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var cachedValue = await GetAsync<T>(key, cancellationToken);

        if (cachedValue is not null)
        {
            _logger.LogDebug("Redis cache hit: {Key}", key);
            return cachedValue;
        }

        _logger.LogDebug("Redis cache miss: {Key}", key);

        var value = await factory(cancellationToken);

        if (value is not null)
        {
            await SetAsync(key, value, expiration, cancellationToken);
        }

        return value;
    }
}
