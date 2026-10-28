namespace FullstackTemplate.Server.Databases;

using Domain;
using Exceptions;
using Microsoft.EntityFrameworkCore;

public static class QueryableExtensions
{
    /// <exception cref="NotFoundException">No row has the ID. An ID that is not valid for the entity, such as one with a
    /// NUL that Postgres text cannot hold, gets no query.</exception>
    public static async Task<TEntity> GetById<TEntity>(
        this IQueryable<TEntity> query,
        string id,
        CancellationToken cancellationToken = default)
        where TEntity : BaseEntity
    {
        var result = EntityId.IsValid(id, IdPrefix<TEntity>.Value)
            ? await query.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            : null;

        return result.MustBeFoundOrThrow();
    }

    public static TEntity MustBeFoundOrThrow<TEntity>(this TEntity? entity)
        where TEntity : BaseEntity
    {
        return entity ?? throw new NotFoundException($"{typeof(TEntity).Name} was not found.");
    }

    // EntityIdPrefixAnalyzer makes sure that each entity has the IdPrefix constant
    private static class IdPrefix<TEntity>
        where TEntity : BaseEntity
    {
        public static readonly string Value = (string?)typeof(TEntity).GetField("IdPrefix")?.GetRawConstantValue()
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} has no public const string IdPrefix.");
    }
}
