using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenLibraryRent.Dtos;

namespace OpenLibraryRent.Controllers;

public abstract class BaseController : ControllerBase
{
    protected async Task<(TEntity? Entity, IActionResult? ErrorResult)> FindEntityOrNotFoundAsync<TEntity>(
        DbSet<TEntity> dbSet,
        Guid id,
        string entityName,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var entity = await dbSet.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null)
        {
            return (null, NotFound(new MessageResponse($"{entityName} not found")));
        }
        return (entity, null);
    }

    protected async Task<(TEntity1? Entity1, TEntity2? Entity2, IActionResult? ErrorResult)> FindEntitiesOrNotFoundAsync<TEntity1, TEntity2>(
        DbSet<TEntity1> dbSet1,
        Guid id1,
        string entityName1,
        DbSet<TEntity2> dbSet2,
        Guid id2,
        string entityName2,
        CancellationToken cancellationToken = default)
        where TEntity1 : class
        where TEntity2 : class
    {
        var entity1 = await dbSet1.FindAsync(new object[] { id1 }, cancellationToken);
        if (entity1 == null)
        {
            return (null, null, NotFound(new MessageResponse($"{entityName1} not found")));
        }

        var entity2 = await dbSet2.FindAsync(new object[] { id2 }, cancellationToken);
        if (entity2 == null)
        {
            return (null, null, NotFound(new MessageResponse($"{entityName2} not found")));
        }

        return (entity1, entity2, null);
    }

    protected IActionResult? ValidateParentChildRelationship<TParent, TChild>(
        TParent parent,
        TChild child,
        Func<TParent, Guid> parentIdSelector,
        Func<TChild, Guid> childParentIdSelector,
        string parentName,
        string childName)
    {
        var parentId = parentIdSelector(parent);
        var childParentId = childParentIdSelector(child);

        if (childParentId != parentId)
        {
            return BadRequest(new MessageResponse($"{childName} does not belong to the {parentName}"));
        }

        return null;
    }

    protected async Task<IActionResult?> CheckDuplicateAsync<TEntity>(
        IQueryable<TEntity> query,
        string entityName,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var exists = await query.AnyAsync(cancellationToken);
        if (exists)
        {
            return Conflict(new MessageResponse($"{entityName} already exists"));
        }
        return null;
    }
}
