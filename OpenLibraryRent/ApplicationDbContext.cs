using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenLibraryRent.Models;

namespace OpenLibraryRent;

/// <summary>
/// アプリケーションDbContext（RLS対応）
/// PostgreSQL Row Level Securityによるテナント分離
/// </summary>
public class ApplicationDbContext : MultiTenantIdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly IMultiTenantContextAccessor _multiTenantContextAccessor;

    public ApplicationDbContext(IMultiTenantContextAccessor multiTenantContextAccessor, DbContextOptions options)
        : base(multiTenantContextAccessor, options)
    {
        _multiTenantContextAccessor = multiTenantContextAccessor;
    }

    // テナント情報（RLSの外）
    public DbSet<ApplicationTenantInfo> Tenants => Set<ApplicationTenantInfo>();
    public DbSet<ApplicationTenantDetail> TenantDetails => Set<ApplicationTenantDetail>();

    // ユーザー・ロール（RLS対象）
    // Note: Users and Roles are inherited from IdentityDbContext

    // 書籍関連（RLS対象）
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookCopy> BookCopies => Set<BookCopy>();
    public DbSet<Rental> Rentals => Set<Rental>();
    public DbSet<RentalHistory> RentalHistories => Set<RentalHistory>();

    // 権限
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    // ユーザー承認リクエスト
    public DbSet<UserApprovalRequest> UserApprovalRequests => Set<UserApprovalRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // テナント情報（RLS対象外）
        modelBuilder.Entity<ApplicationTenantInfo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Identifier).IsUnique();
            entity.HasIndex(e => e.CreatorEmail);
            entity.HasOne(e => e.Detail)
                .WithOne(d => d.Tenant)
                .HasForeignKey<ApplicationTenantDetail>(d => d.TenantId);
        });

        modelBuilder.Entity<ApplicationTenantDetail>(entity =>
        {
            entity.HasKey(e => e.TenantId);
        });

        // ユーザー設定
        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(u => new { u.TenantId, u.Sub })
            .IsUnique();

        // ロール権限設定
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RoleId, e.Name }).IsUnique();
            entity.HasOne(e => e.Role)
                .WithMany(r => r.Permissions)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 書籍設定
        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasIndex(b => b.Isbn);
            entity.HasIndex(b => b.Title);
            entity.HasMany(b => b.Copies)
                .WithOne(c => c.Book)
                .HasForeignKey(c => c.BookId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(b => b.Rentals)
                .WithOne(r => r.Book)
                .HasForeignKey(r => r.BookId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 書籍個体設定
        modelBuilder.Entity<BookCopy>(entity =>
        {
            entity.HasIndex(c => c.InventoryCode).IsUnique();
            entity.HasOne(c => c.CurrentRental)
                .WithOne(r => r.BookCopy)
                .HasForeignKey<Rental>(r => r.BookCopyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(c => c.RentalHistories)
                .WithOne(h => h.BookCopy)
                .HasForeignKey(h => h.BookCopyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 貸出設定
        modelBuilder.Entity<Rental>(entity =>
        {
            entity.HasIndex(r => r.UserId);
            entity.HasIndex(r => r.Status);
            entity.HasIndex(r => r.DueDate);
            entity.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 貸出履歴設定
        modelBuilder.Entity<RentalHistory>(entity =>
        {
            entity.HasIndex(h => h.UserId);
            entity.HasIndex(h => h.ReturnedAt);
            entity.HasOne(h => h.User)
                .WithMany()
                .HasForeignKey(h => h.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(h => h.Book)
                .WithMany()
                .HasForeignKey(h => h.BookId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ユーザー承認リクエスト設定
        modelBuilder.Entity<UserApprovalRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.Email });
            entity.HasIndex(e => new { e.TenantId, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.Sub });
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenantId();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyTenantId();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyTenantId()
    {
        var tenantInfo = _multiTenantContextAccessor.MultiTenantContext?.TenantInfo as ApplicationTenantInfo;
        var tenantId = tenantInfo?.Id;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            var tenantProperty = entry.Properties.FirstOrDefault(p =>
                p.Metadata.Name == "TenantId" && p.Metadata.ClrType == typeof(string));

            if (tenantProperty != null && tenantProperty.CurrentValue == null)
            {
                tenantProperty.CurrentValue = tenantId;
            }
        }
    }
}
