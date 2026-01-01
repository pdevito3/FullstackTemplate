namespace FullstackTemplate.Server.Domain.Tenants.Features;

using Databases;
using MediatR;

public static class DeleteTenant
{
    public sealed record Command(Guid Id) : IRequest;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command>
    {
        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            var tenant = await dbContext.Tenants.GetById(request.Id, cancellationToken);

            dbContext.Tenants.Remove(tenant);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
