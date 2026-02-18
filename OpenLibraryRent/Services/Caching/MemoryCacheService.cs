using Microsoft.Extensions.Caching.Memory;

namespace OpenLibraryRent.Services.Caching;

/// <summary>
/// インメモリキャッシュの実装
/// デフォルトのキャッシュサービス
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;

    // 削除用にキーを追跡
    private readonly HashSet<string> _keys = new();
    private readonly object _keysLock = new();

    public MemoryCacheService(IMemoryCache cache, ILogger<MemoryCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = _cache.TryGetValue(key, out T? cachedValue) ? cachedValue : default;
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var options = new MemoryCacheEntryOptions();

        if (expiration.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = expiration;
        }
        else
        {
            options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30); // デフォルト30分
        }

        // キーの追跡
        options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
        {
            lock (_keysLock)
            {
                _keys.Remove(evictedKey.ToString() ?? string.Empty);
            }
        });

        lock (_keysLock)
        {
            _keys.Add(key);
        }

        _cache.Set(key, value, options);

        _logger.LogDebug("Cache set: {Key}, Expiration: {Expiration}", key, expiration);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.Remove(key);

        lock (_keysLock)
        {
            _keys.Remove(key);
        }

        _logger.LogDebug("Cache removed: {Key}", key);

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // パターンマッチング（簡易実装：前方一致と*ワイルドカード）
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*") + "$";
        var regex = new System.Text.RegularExpressions.Regex(regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        List<string> keysToRemove;
        lock (_keysLock)
        {
            keysToRemove = _keys.Where(k => regex.IsMatch(k)).ToList();
        }

        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
            lock (_keysLock)
            {
                _keys.Remove(key);
            }
        }

        _logger.LogDebug("Cache removed by pattern: {Pattern}, Count: {Count}", pattern, keysToRemove.Count);

        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out T? cachedValue) && cachedValue is not null)
        {
            _logger.LogDebug("Cache hit: {Key}", key);
            return cachedValue;
        }

        _logger.LogDebug("Cache miss: {Key}", key);

        var value = await factory(cancellationToken);

        if (value is not null)
        {
            await SetAsync(key, value, expiration, cancellationToken);
        }

        return value;
    }
}
