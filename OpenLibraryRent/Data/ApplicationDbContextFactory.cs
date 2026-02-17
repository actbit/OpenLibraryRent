using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenLibraryRent.Models;

namespace OpenLibraryRent.Data;

/// <summary>
/// Design-time DbContext Factory for EF Core migrations
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var tenantInfo = new ApplicationTenantInfo("migration-dummy", "migration-dummy")
        {
            Detail = new ApplicationTenantDetail
            {
                TenantId = "migration-dummy"
            }
        };

        var accessor = new DesignTimeMultiTenantContextAccessor(tenantInfo);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=openlibraryrent;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(accessor, optionsBuilder.Options);
    }
}

/// <summary>
/// Design-time用のダミーMultiTenantContextAccessor
/// </summary>
internal class DesignTimeMultiTenantContextAccessor : IMultiTenantContextAccessor
{
    public IMultiTenantContext MultiTenantContext { get; set; }

    public DesignTimeMultiTenantContextAccessor(ApplicationTenantInfo tenantInfo)
    {
        MultiTenantContext = new MultiTenantContext<ApplicationTenantInfo>()
        {
            TenantInfo = tenantInfo
        };
    }

    public void SetTenantContext(IMultiTenantContext context)
    {
        MultiTenantContext = context;
    }
}
