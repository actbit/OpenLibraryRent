using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OpenLibraryRent.Models;

namespace OpenLibraryRent.Services;

/// <summary>
/// EF Core を使用したマルチテナント Store
/// ApplicationDbContext から tenant 情報を取得（RLS用の単一DB構成）
/// </summary>
public class EFCoreMultiTenantStore : IMultiTenantStore<ApplicationTenantInfo>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<EFCoreMultiTenantStore> _logger;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    public EFCoreMultiTenantStore(
        ApplicationDbContext dbContext,
        ILogger<EFCoreMultiTenantStore> logger,
        IMemoryCache cache)
    {
        _dbContext = dbContext;
        _logger = logger;
        _cache = cache;
    }

    public Task<bool> AddAsync(ApplicationTenantInfo tenantInfo)
    {
        throw new NotImplementedException("Use TenantManagementService.CreateTenantAsync instead");
    }

    public Task<bool> UpdateAsync(ApplicationTenantInfo tenantInfo)
    {
        throw new NotImplementedException("Use API endpoints to update tenant");
    }

    public Task<bool> RemoveAsync(string identifier)
    {
        throw new NotImplementedException("Use TenantManagementService.DeleteTenantAsync instead");
    }

    public Task<ApplicationTenantInfo?> GetByIdentifierAsync(string identifier)
    {
        return TryGetAsync(identifier);
    }

    public Task<ApplicationTenantInfo?> GetAsync(string id)
    {
        return TryGetByIdAsync(id);
    }

    public async Task<ApplicationTenantInfo?> TryGetAsync(string identifier)
    {
        var cacheKey = $"tenant:id:{identifier}";

        if (_cache.TryGetValue<ApplicationTenantInfo?>(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            var tenant = await _dbContext.Tenants
                .Include(t => t.Detail)
                .FirstOrDefaultAsync(t => t.Identifier == identifier);

            if (tenant == null)
            {
                return null;
            }

            _cache.Set(cacheKey, tenant, CacheExpiration);

            return tenant;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenant by identifier: {Identifier}", identifier);
            throw;
        }
    }

    public async Task<ApplicationTenantInfo?> TryGetByIdentifierAsync(string identifier)
    {
        return await TryGetAsync(identifier);
    }

    public async Task<ApplicationTenantInfo?> TryGetByIdAsync(string id)
    {
        var cacheKey = $"tenant:id-guid:{id}";

        if (_cache.TryGetValue<ApplicationTenantInfo?>(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            var tenant = await _dbContext.Tenants
                .Include(t => t.Detail)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tenant != null)
            {
                _cache.Set(cacheKey, tenant, CacheExpiration);
            }

            return tenant;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenant by id: {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<ApplicationTenantInfo>> GetAllAsync()
    {
        try
        {
            return await _dbContext.Tenants
                .Include(t => t.Detail)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all tenants");
            throw;
        }
    }

    public async Task<IEnumerable<ApplicationTenantInfo>> GetAllAsync(int pageNumber, int pageSize)
    {
        try
        {
            return await _dbContext.Tenants
                .Include(t => t.Detail)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenants with pagination");
            throw;
        }
    }

    public Task<bool> TryAddAsync(ApplicationTenantInfo tenantInfo)
    {
        throw new NotImplementedException("Use TenantManagementService.CreateTenantAsync instead");
    }

    public Task<bool> TryUpdateAsync(ApplicationTenantInfo tenantInfo)
    {
        throw new NotImplementedException("Use API endpoints to update tenant");
    }

    public Task<bool> TryRemoveAsync(string identifier)
    {
        throw new NotImplementedException("Use TenantManagementService.DeleteTenantAsync instead");
    }
}
