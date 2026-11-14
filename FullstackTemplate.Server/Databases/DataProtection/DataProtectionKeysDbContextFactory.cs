namespace FullstackTemplate.Server.Databases.DataProtection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>The context for the EF Core tools, so that <c>dotnet ef migrations add</c> does not start the Server.</summary>
public sealed class DataProtectionKeysDbContextFactory : IDesignTimeDbContextFactory<DataProtectionKeysDbContext>
{
    public DataProtectionKeysDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DataProtectionKeysDbContext>();
        DataProtectionKeyRing.ConfigureKeysDb(options, "Host=localhost;Database=fullstacktemplate");
        return new DataProtectionKeysDbContext(options.Options);
    }
}
