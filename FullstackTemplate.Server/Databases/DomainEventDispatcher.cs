namespace FullstackTemplate.Server.Databases;

using System.Collections.Concurrent;
using System.Reflection;
using Domain;
using Exceptions;
using Resources;

public sealed class DomainEventDispatcher(IServiceProvider serviceProvider)
{
    private delegate Task RunHandlers(
        IServiceProvider serviceProvider, DomainEvent domainEvent, AppDbContext dbContext, CancellationToken cancellationToken);

    private static readonly MethodInfo RunHandlersOfType = typeof(DomainEventDispatcher)
        .GetMethod(nameof(RunHandlersAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly ConcurrentDictionary<Type, RunHandlers> RunnersByEventType = new();

    public Task DispatchAsync(DomainEvent domainEvent, AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var run = RunnersByEventType.GetOrAdd(domainEvent.GetType(),
            eventType => RunHandlersOfType.MakeGenericMethod(eventType).CreateDelegate<RunHandlers>());
        return run(serviceProvider, domainEvent, dbContext, cancellationToken);
    }

    private static async Task RunHandlersAsync<TEvent>(
        IServiceProvider serviceProvider, DomainEvent domainEvent, AppDbContext dbContext, CancellationToken cancellationToken)
        where TEvent : DomainEvent
    {
        foreach (var handler in serviceProvider.GetServices<IDomainEventHandler<TEvent>>())
            await RunHandlerAsync(handler, (TEvent)domainEvent, dbContext, cancellationToken);
    }

    private static async Task RunHandlerAsync<TEvent>(
        IDomainEventHandler<TEvent> handler, TEvent domainEvent, AppDbContext dbContext, CancellationToken cancellationToken)
        where TEvent : DomainEvent
    {
        var eventType = typeof(TEvent).Name;
        var handlerType = handler.GetType().Name;
        using var activity = Telemetry.Trace(
            [("app.domain_event.type", eventType), ("app.domain_event.handler", handlerType)],
            "domain_event.handle");

        try
        {
            await handler.Handle(domainEvent, dbContext, cancellationToken);
        }
        catch (Exception exception)
        {
            activity?.SetTag("error.type", exception.GetType().FullName);

            if (!DomainError.IsClientError(exception))
                Telemetry.MarkFailed(activity, exception);

            throw;
        }
    }
}
