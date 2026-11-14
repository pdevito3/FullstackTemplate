namespace FullstackTemplate.Server.Examples.WelcomeEmails;

using Databases;
using Domain;
using Domain.Users.DomainEvents;

/// <summary>A new user gets a welcome email after the commit of the save that adds the user.</summary>
public sealed class QueueWelcomeEmailWhenUserIsCreated : IDomainEventHandler<UserCreated>
{
    public async Task Handle(UserCreated domainEvent, AppDbContext dbContext, CancellationToken cancellationToken)
    {
        // The save adds the user, so a query does not see it yet. FindAsync reads the tracked entity.
        var user = await dbContext.Users.FindAsync([domainEvent.UserId], cancellationToken)
            ?? throw new InvalidOperationException($"The save of user {domainEvent.UserId} does not track the user.");

        dbContext.QueueJob(new SendWelcomeEmail(user.TenantId, user.Id));
    }
}
