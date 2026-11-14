namespace FullstackTemplate.Server.Databases.DataProtection;

using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The key ring of ASP.NET Data Protection on the Server, in the schema <see cref="Schema"/> of appdb. The
/// antiforgery tokens of the jobs dashboard use it. Each Server process reads the same keys, so a token from one
/// process is valid on another. It is not <see cref="AppDbContext"/>, because the EF Core key repository saves with
/// the sync <c>SaveChanges</c>, and AppDbContext refuses a sync save.
/// </summary>
public sealed class DataProtectionKeysDbContext(DbContextOptions<DataProtectionKeysDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public const string Schema = "data_protection";

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasDefaultSchema(Schema);
}
