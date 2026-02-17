using System.Linq.Expressions;
using OpenLibraryRent.Models;

namespace OpenLibraryRent.Repositories;

public interface IBaseRepository<T> where T : BaseModel
{
    IQueryable<T> Query();
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Delete(T entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
