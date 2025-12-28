namespace FullstackTemplate.Server.Databases;

using Domain;
using Exceptions;
using Microsoft.EntityFrameworkCore;

public static class QueryableExtensions
{
    public static async Task<TEntity?> GetByIdOrDefault<TEntity>(
        this DbSet<TEntity> dbSet,
        Guid id,
        CancellationToken cancellationToken = default)
        where TEntity : BaseEntity
    {
        return await dbSet.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public static async Task<TEntity?> GetByIdOrDefault<TEntity>(
        this IQueryable<TEntity> query,
        Guid id,
        CancellationToken cancellationToken = default)
        where TEntity : BaseEntity
    {
        return await query.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public static async Task<TEntity> GetById<TEntity>(
        this DbSet<TEntity> dbSet,
        Guid id,
        CancellationToken cancellationToken = default)
        where TEntity : BaseEntity
    {
        var result = await dbSet.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        return result.MustBeFoundOrThrow();
    }

    public static async Task<TEntity> GetById<TEntity>(
        this IQueryable<TEntity> query,
        Guid id,
        CancellationToken cancellationToken = default)
        where TEntity : BaseEntity
    {
        var result = await query.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        return result.MustBeFoundOrThrow();
    }

    public static TEntity MustBeFoundOrThrow<TEntity>(this TEntity? entity)
        where TEntity : BaseEntity
    {
        return entity ?? throw new NotFoundException($"{typeof(TEntity).Name} was not found.");
    }
}
