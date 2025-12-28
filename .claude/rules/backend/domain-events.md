# Domain Events

Guidelines for implementing domain events to notify the system of important business occurrences.

## Directory Structure

Domain events are organized within each domain entity folder:

```
Domain/
└── [EntityName]/
    └── DomainEvents/
        ├── [EntityName]Created.cs
        ├── [EntityName]Updated.cs
        └── [BusinessEvent].cs     # Domain-specific events
```

Note: We do not create domain events for deletion operations. Soft deletes are handled via DbContext interception.

## Domain Event Structure

### Base DomainEvent Record

The base `DomainEvent` is a simple abstract record inheriting from `INotification`:

```csharp
namespace FullstackTemplate.Server.Domain;

using MediatR;

public abstract record DomainEvent : INotification;
```

### Basic Event Template

Domain events are sealed records extending `DomainEvent`:

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.DomainEvents;

public sealed record [EntityName]Created([EntityName] [EntityName]) : DomainEvent;

public sealed record [EntityName]Updated(Guid Id) : DomainEvent;
```

### Business-Specific Events

```csharp
public sealed record OrderSubmitted(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount) : DomainEvent;

public sealed record PaymentReceived(
    Guid OrderId,
    Guid PaymentId,
    decimal Amount,
    DateTimeOffset ReceivedAt) : DomainEvent;
```

## Queuing Domain Events

Domain events are queued within entity methods and dispatched after persistence.

### In Domain Entity

```csharp
public class Order : BaseEntity
{
    public static Order Create(OrderForCreation orderForCreation)
    {
        var order = new Order
        {
            // ... property assignments
        };

        order.QueueDomainEvent(new OrderCreated(order));

        return order;
    }

    public Order Submit()
    {
        GuardAgainstInvalidSubmission();

        Status = OrderStatus.Submitted();

        QueueDomainEvent(new OrderSubmitted(Id, CustomerId, CalculateTotal()));

        return this;
    }

    public Order Cancel(string reason)
    {
        GuardAgainstInvalidCancellation();

        Status = OrderStatus.Cancelled();
        CancellationReason = reason;

        QueueDomainEvent(new OrderCancelled(Id, reason, DateTimeOffset.UtcNow));

        return this;
    }
}
```

### BaseEntity Support

The `BaseEntity` class provides infrastructure for domain events:

```csharp
public abstract class BaseEntity
{
    private readonly List<DomainEvent> _domainEvents = [];

    [NotMapped]
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void QueueDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
```

## Domain Event Handlers

Handlers react to domain events using MediatR notifications.

### Handler Structure

```csharp
namespace FullstackTemplate.Server.Domain.Orders.DomainEvents;

public sealed class OrderSubmittedHandler : INotificationHandler<OrderSubmitted>
{
    private readonly IEmailService _emailService;
    private readonly IInventoryService _inventoryService;

    public OrderSubmittedHandler(
        IEmailService emailService,
        IInventoryService inventoryService)
    {
        _emailService = emailService;
        _inventoryService = inventoryService;
    }

    public async Task Handle(OrderSubmitted notification, CancellationToken cancellationToken)
    {
        // Send confirmation email
        await _emailService.SendOrderConfirmation(notification.OrderId, cancellationToken);

        // Reserve inventory
        await _inventoryService.ReserveItems(notification.OrderId, cancellationToken);
    }
}
```

### Multiple Handlers

Multiple handlers can respond to the same event:

```csharp
// Handler 1: Send notification
public sealed class SendOrderNotificationHandler : INotificationHandler<OrderSubmitted>
{
    public async Task Handle(OrderSubmitted notification, CancellationToken cancellationToken)
    {
        // Send push notification
    }
}

// Handler 2: Update analytics
public sealed class UpdateOrderAnalyticsHandler : INotificationHandler<OrderSubmitted>
{
    public async Task Handle(OrderSubmitted notification, CancellationToken cancellationToken)
    {
        // Record analytics event
    }
}

// Handler 3: Start fulfillment workflow
public sealed class StartFulfillmentHandler : INotificationHandler<OrderSubmitted>
{
    public async Task Handle(OrderSubmitted notification, CancellationToken cancellationToken)
    {
        // Create fulfillment task
    }
}
```

## Event Dispatch

Domain events are automatically dispatched by `AppDbContext.SaveChangesAsync()`. The DbContext collects all domain events from tracked entities, saves changes, and then publishes each event via MediatR.

```csharp
public class AppDbContext : DbContext
{
    private readonly IMediator _mediator;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(cancellationToken);
        await DispatchDomainEvents();
        return result;
    }

    private async Task DispatchDomainEvents()
    {
        var domainEventEntities = ChangeTracker.Entries<BaseEntity>()
            .Select(po => po.Entity)
            .Where(po => po.DomainEvents.Any())
            .ToArray();

        foreach (var entity in domainEventEntities)
        {
            var events = entity.DomainEvents.ToArray();
            entity.ClearDomainEvents();
            foreach (var domainEvent in events)
                await _mediator.Publish(domainEvent);
        }
    }
}
```

This means handlers simply call `SaveChangesAsync()` and events are dispatched automatically:

```csharp
public async Task<OrderDto> Handle(Command request, CancellationToken ct)
{
    var order = await dbContext.Orders.GetById(request.Id, ct);

    order.Submit();  // Queues domain event

    await dbContext.SaveChangesAsync(ct);  // Events dispatched automatically

    return order.ToOrderDto();
}
```

## Key Principles

### 1. Events Are Sealed Records

Use sealed records extending `DomainEvent`:

```csharp
public sealed record OrderShipped(
    Guid OrderId,
    string TrackingNumber,
    DateTimeOffset ShippedAt) : DomainEvent;
```

### 2. Events Describe What Happened

Name events in past tense to describe completed actions:

```csharp
// Good - past tense, describes what happened
OrderCreated
OrderSubmitted
PaymentReceived
CustomerRegistered

// Bad - imperative or present tense
CreateOrder
SubmitOrder
ReceivePayment
```

### 3. Events Are Self-Contained

Include all necessary information in the event:

```csharp
// Good - self-contained
public sealed record OrderSubmitted(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    int ItemCount) : DomainEvent;

// Avoid - requiring database lookups in handlers
public sealed record OrderSubmitted(Guid OrderId) : DomainEvent;  // Handlers must query for details
```

### 4. Queue Events at State Changes

Queue events when the entity's state changes meaningfully:

```csharp
public Order Submit()
{
    // State change
    Status = OrderStatus.Submitted();

    // Queue event to notify system
    QueueDomainEvent(new OrderSubmitted { ... });

    return this;
}
```

### 5. Handlers Are Independent

Each handler should be independent and idempotent:

```csharp
// Good - independent handler
public sealed class SendEmailHandler : INotificationHandler<OrderSubmitted>
{
    public async Task Handle(OrderSubmitted notification, CancellationToken ct)
    {
        // Check if email already sent (idempotent)
        if (await _emailLog.ExistsAsync(notification.OrderId, ct))
            return;

        await _emailService.SendAsync(...);
        await _emailLog.RecordAsync(notification.OrderId, ct);
    }
}
```

### 6. Use Primary Constructor Parameters

Record primary constructors ensure all properties are initialized:

```csharp
public sealed record CustomerRegistered(
    Guid CustomerId,
    string Email,
    DateTimeOffset RegisteredAt) : DomainEvent;
```

## Common Event Patterns

### Created Event with Full Entity

For new entity creation, include the full entity:

```csharp
public sealed record CustomerCreated(Customer Customer) : DomainEvent;
```

### Updated Event with Changed Fields

For updates, include only what's needed:

```csharp
// Simple - just the ID
public sealed record CustomerUpdated(Guid Id) : DomainEvent;

// Detailed - include changed fields
public sealed record CustomerAddressUpdated(
    Guid CustomerId,
    Address OldAddress,
    Address NewAddress) : DomainEvent;
```

### Business Action Events

For domain-specific operations:

```csharp
public sealed record SubscriptionRenewed(
    Guid SubscriptionId,
    Guid CustomerId,
    DateOnly NewExpirationDate,
    decimal AmountCharged) : DomainEvent;

public sealed record InventoryReplenished(
    Guid ProductId,
    int QuantityAdded,
    int NewTotalQuantity) : DomainEvent;
```

## Anti-Patterns to Avoid

### Don't Dispatch Events Manually

Events are automatically dispatched by `SaveChangesAsync()`. Don't dispatch them manually:

```csharp
// Bad - manual event dispatch
public async Task Handle(Command request, CancellationToken ct)
{
    var order = Order.Create(...);
    await mediator.Publish(new OrderCreated(order));  // Don't do this!
    await dbContext.Orders.AddAsync(order);
    await dbContext.SaveChangesAsync(ct);
}

// Good - let SaveChangesAsync dispatch events automatically
public async Task Handle(Command request, CancellationToken ct)
{
    var order = Order.Create(...);  // Queues OrderCreated event
    await dbContext.Orders.AddAsync(order);
    await dbContext.SaveChangesAsync(ct);  // Events dispatched automatically
}
```

### Don't Create Events Outside Entities

```csharp
// Bad - event created in handler
public async Task Handle(Command request, CancellationToken ct)
{
    var order = await dbContext.Orders.GetById(request.Id);
    order.Status = OrderStatus.Submitted();  // Direct property mutation!
    await mediator.Publish(new OrderSubmitted(...));  // Event not from entity
}

// Good - entity creates its own events
public async Task Handle(Command request, CancellationToken ct)
{
    var order = await dbContext.Orders.GetById(request.Id);
    order.Submit();  // Entity queues OrderSubmitted event
    await dbContext.SaveChangesAsync(ct);
}
```

### Don't Use Events for Synchronous Validation

```csharp
// Bad - using events for validation
public sealed class ValidateOrderHandler : INotificationHandler<OrderSubmitting>
{
    public Task Handle(OrderSubmitting notification, CancellationToken ct)
    {
        if (!IsValid(notification.Order))
            throw new ValidationException("Invalid order");
        return Task.CompletedTask;
    }
}

// Good - validation in domain entity
public Order Submit()
{
    if (Items.Count == 0)
        throw new ValidationException("Order must have items");

    Status = OrderStatus.Submitted();
    QueueDomainEvent(new OrderSubmitted(Id, CustomerId, CalculateTotal()));
    return this;
}
```
