using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenLibraryRent.Services.Caching;

/// <summary>
/// Aspire環境用のキャッシュ拡張メソッド
/// </summary>
public static class AspireCacheExtensions
{
    /// <summary>
    /// Aspire統合でキャッシュサービスを登録
    /// RedisがAspireリソースとして設定されている場合に使用
    /// </summary>
    /// <param name="services">サービスコレクション</param>
    /// <param name="configuration">設定</param>
    /// <param name="connectionName">Aspire接続名（デフォルト: "redis"）</param>
    public static IServiceCollection AddCacheServiceWithAspire(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionName = "redis")
    {
        // Aspire環境かどうかを確認（OTEL_EXPORTER_OTLP_ENDPOINTの存在で判定）
        var isAspireEnvironment = !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        // Redis接続文字列の確認
        var redisConnectionString = configuration.GetConnectionString(connectionName);

        if (isAspireEnvironment && !string.IsNullOrEmpty(redisConnectionString))
        {
            // Aspire + Redis: AspireのRedis統合を使用
            // Note: AddRedisDistributedCacheは接続文字列を自動設定
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "OpenLibraryRent:";
            });

            services.AddSingleton<ICacheService, RedisCacheService>();
        }
        else if (!string.IsNullOrEmpty(redisConnectionString))
        {
            // Redis単体（Aspire以外）
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "OpenLibraryRent:";
            });

            services.AddSingleton<ICacheService, RedisCacheService>();
        }
        else
        {
            // インメモリキャッシュ（デフォルト）
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, MemoryCacheService>();
        }

        return services;
    }
}
