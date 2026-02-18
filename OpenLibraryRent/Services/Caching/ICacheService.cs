namespace OpenLibraryRent.Services.Caching;

/// <summary>
/// キャッシュサービスのインターフェース
/// インメモリキャッシュとRedisキャッシュを抽象化
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// キャッシュから値を取得
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// キャッシュに値を設定
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// キャッシュから値を削除
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// キャッシュの存在確認
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// パターンに一致するキーを一括削除
    /// </summary>
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// 値を取得、存在しない場合はfactoryで作成してキャッシュに保存
    /// </summary>
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
}
