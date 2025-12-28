namespace FullstackTemplate.Server.Databases;

using System.Security.Claims;
using MediatR;
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
            new DesignTimeMediator());
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
        public bool IsMachine => false;
        public bool IsAuthenticated => false;
        public IReadOnlyList<string> Roles => [];
        public bool IsInRole(string role) => false;
        public string? GetClaimValue(string claimType) => null;
        public IReadOnlyList<string> GetClaimValues(string claimType) => [];
    }

    private sealed class DesignTimeMediator : IMediator
    {
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => Task.CompletedTask;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default)
            where TRequest : IRequest => throw new NotImplementedException();
        public Task<object?> Send(object request, CancellationToken ct = default)
            => throw new NotImplementedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default)
            => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
