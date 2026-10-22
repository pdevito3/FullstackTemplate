using FullstackTemplate.Bff.BackChannel;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FullstackTemplate.Bff.Sessions;

/// <summary>
/// The context of <c>bffdb</c>: the BFF sessions, the Data Protection keys, and the replay cache of the logout
/// tokens. It has its own migrations in <c>Sessions/Migrations</c>, which the BFF applies at startup.
/// </summary>
public sealed class BffDbContext(DbContextOptions<BffDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    /// <summary>
    /// The name of the connection string, and of the database resource in the AppHost.
    /// </summary>
    public const string DatabaseName = "bffdb";

    public DbSet<BffSession> Sessions => Set<BffSession>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<UsedLogoutToken> UsedLogoutTokens => Set<UsedLogoutToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BffDbContext).Assembly);
        // The Data Protection package has no configuration class. The table only needs to exist.
        modelBuilder.Entity<DataProtectionKey>();
    }
}
