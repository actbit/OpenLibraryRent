using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using OpenLibraryRent.Models;

namespace OpenLibraryRent.Repositories;

public class BaseRepository<T> : IBaseRepository<T> where T : BaseModel
{
    protected readonly ApplicationDbContext Context;
    protected readonly DbSet<T> Entities;

    public BaseRepository(ApplicationDbContext context)
    {
        Context = context;
        Entities = Context.Set<T>();
    }

    public IQueryable<T> Query() => Entities.AsQueryable();

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Entities.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public virtual Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var query = predicate == null ? Entities : Entities.Where(predicate);
        return query.ToListAsync(cancellationToken);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await Entities.AddAsync(entity, cancellationToken);
        return entity;
    }

    public virtual void Update(T entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        Entities.Update(entity);
    }

    public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        Entities.Update(entity);
        await SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual void Delete(T entity) => Entities.Remove(entity);

    public virtual async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        Entities.Remove(entity);
        await SaveChangesAsync(cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Context.SaveChangesAsync(cancellationToken);
}
