namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using MediatR;

public static class DeleteUser
{
    public sealed record Command(Guid Id) : IRequest;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command>
    {
        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await dbContext.Users.GetById(request.Id, cancellationToken);

            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
