namespace FullstackTemplate.Server.Databases;

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Services;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=fullstacktemplate")
            .UseSnakeCaseNamingConvention();

        return new AppDbContext(
            optionsBuilder.Options,
            TimeProvider.System,
            new DesignTimeCurrentUserService(),
            new DesignTimeCallerIdentityResolver(),
            new DesignTimeTenantScopeProvider(),
            new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()));
    }

    private sealed class DesignTimeCurrentUserService : ICurrentUserService
    {
        public ClaimsPrincipal? User => null;
        public string? UserIdentifier => null;
        public string? Email => null;
        public string? FirstName => null;
        public string? LastName => null;
        public string? Name => null;
        public string? Username => null;
        public string? ClientId => null;
        public bool IsAuthenticated => false;
        public IReadOnlyList<string> Roles => [];
        public bool IsInRole(string role) => false;
        public string? GetClaimValue(string claimType) => null;
        public IReadOnlyList<string> GetClaimValues(string claimType) => [];
    }

    private sealed class DesignTimeCallerIdentityResolver : ICallerIdentityResolver
    {
        public CallerIdentity Current { get; } = new CallerIdentity.System();
        public Task<CallerIdentity> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Current);
    }

    // The EF Core tools are system work, so the design-time context opts in to all tenants.
    private sealed class DesignTimeTenantScopeProvider : ITenantScopeProvider
    {
        public TenantScope GetCurrentScope() => TenantScope.AllTenants;
    }
}
