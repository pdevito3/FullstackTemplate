using Microsoft.EntityFrameworkCore;

namespace FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

/// <summary>EntityGraph conveniences for <see cref="DbContext" />.</summary>
public static class DbContextExtensions
{
    extension(DbContext db)
    {
        /// <summary>Wraps the context as a graph store, so wrappers can write <c>EntityGraph.Start(seed, options, db.AsGraphStore())</c>.</summary>
        public DbContextStore AsGraphStore() => new(db);
    }
}
