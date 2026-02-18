using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenLibraryRent.Services.Caching;

/// <summary>
/// キャッシュサービスのDI拡張メソッド
/// </summary>
public static class CacheServiceExtensions
{
    /// <summary>
    /// キャッシュサービスを登録
    /// Redis接続文字列が設定されている場合はRedis、そうでなければインメモリを使用
    /// </summary>
    public static IServiceCollection AddCacheService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("redis");

        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            // Redis使用
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "OpenLibraryRent:";
            });

            services.AddSingleton<ICacheService, RedisCacheService>();

            // DataProtectionキーもRedisに保存（オプション）
            // services.AddDataProtection()
            //     .PersistKeysToStackExchangeRedis(redis, "OpenLibraryRent:DataProtection-Keys");
        }
        else
        {
            // インメモリキャッシュ使用（デフォルト）
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, MemoryCacheService>();
        }

        return services;
    }

    /// <summary>
    /// インメモリキャッシュを明示的に使用
    /// </summary>
    public static IServiceCollection AddMemoryCacheService(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();
        return services;
    }

    /// <summary>
    /// Redisキャッシュを明示的に使用（Aspire統合用）
    /// </summary>
    /// <remarks>
    /// Aspire環境では、AddRedisDistributedCacheが接続文字列を自動設定します。
    /// このメソッドはRedisキャッシュサービスを登録します。
    /// </remarks>
    public static IServiceCollection AddRedisCacheService(this IServiceCollection services)
    {
        services.AddSingleton<ICacheService, RedisCacheService>();
        return services;
    }
}

/// <summary>
/// キャッシュキーの定数
/// </summary>
public static class CacheKeys
{
    private const string Prefix = "OpenLibraryRent";

    /// <summary>
    /// テナント情報キャッシュ
    /// </summary>
    public static string TenantInfo(string tenantId) => $"{Prefix}:tenant:{tenantId}:info";

    /// <summary>
    /// テナント設定キャッシュ
    /// </summary>
    public static string TenantSettings(string tenantId) => $"{Prefix}:tenant:{tenantId}:settings";

    /// <summary>
    /// 書籍情報キャッシュ（Open Library APIレスポンス）
    /// </summary>
    public static string BookByIsbn(string isbn) => $"{Prefix}:book:isbn:{isbn}";

    /// <summary>
    /// ユーザー権限キャッシュ
    /// </summary>
    public static string UserPermissions(string tenantId, string userId) =>
        $"{Prefix}:tenant:{tenantId}:user:{userId}:permissions";

    /// <summary>
    /// テナント関連の全キャッシュ
    /// </summary>
    public static string TenantAll(string tenantId) => $"{Prefix}:tenant:{tenantId}:*";
}
