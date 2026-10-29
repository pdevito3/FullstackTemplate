namespace FullstackTemplate.Server.Databases;

using System.Text;
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
        return entity ?? throw new NotFoundException($"{InWords(typeof(TEntity).Name)} was not found.");
    }

    // A message names the entity in words, so WorkOrder is "Work order" (docs/messages.md)
    private static string InWords(string typeName)
    {
        var words = new StringBuilder(typeName.Length + 4);
        for (var i = 0; i < typeName.Length; i++)
        {
            if (i > 0 && char.IsUpper(typeName[i]) && char.IsLower(typeName[i - 1]))
                words.Append(' ').Append(char.ToLowerInvariant(typeName[i]));
            else
                words.Append(typeName[i]);
        }

        return words.ToString();
    }

    // EntityIdPrefixAnalyzer makes sure that each entity has the IdPrefix constant
    private static class IdPrefix<TEntity>
        where TEntity : BaseEntity
    {
        public static readonly string Value = (string?)typeof(TEntity).GetField("IdPrefix")?.GetRawConstantValue()
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} has no public const string IdPrefix.");
    }
}
