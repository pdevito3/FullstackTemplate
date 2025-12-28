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
        ├── [EntityName]Deleted.cs
        └── [BusinessEvent].cs     # Domain-specific events
```

## Domain Event Structure

### Basic Event Template

```csharp
namespace FullstackTemplate.Server.Domain.[EntityName]s.DomainEvents;

public sealed record [EntityName]Created : IDomainEvent
{
    public required [EntityName] [EntityName] { get; init; }
}

public sealed record [EntityName]Updated : IDomainEvent
{
    public required Guid Id { get; init; }
}

public sealed record [EntityName]Deleted : IDomainEvent
{
    public required Guid Id { get; init; }
}
```

### Business-Specific Events

```csharp
public sealed record OrderSubmitted : IDomainEvent
{
    public required Guid OrderId { get; init; }
    public required Guid CustomerId { get; init; }
    public required decimal TotalAmount { get; init; }
}

public sealed record PaymentReceived : IDomainEvent
{
    public required Guid OrderId { get; init; }
    public required Guid PaymentId { get; init; }
    public required decimal Amount { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }
}
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

        order.QueueDomainEvent(new OrderCreated { Order = order });

        return order;
    }

    public Order Submit()
    {
        GuardAgainstInvalidSubmission();

        Status = OrderStatus.Submitted();

        QueueDomainEvent(new OrderSubmitted
        {
            OrderId = Id,
            CustomerId = CustomerId,
            TotalAmount = CalculateTotal()
        });

        return this;
    }

    public Order Cancel(string reason)
    {
        GuardAgainstInvalidCancellation();

        Status = OrderStatus.Cancelled();
        CancellationReason = reason;

        QueueDomainEvent(new OrderCancelled
        {
            OrderId = Id,
            Reason = reason,
            CancelledAt = DateTimeOffset.UtcNow
        });

        return this;
    }
}
```

### BaseEntity Support

The `BaseEntity` class provides infrastructure for domain events:

```csharp
public abstract class BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    [NotMapped]
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void QueueDomainEvent(IDomainEvent domainEvent)
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

Domain events are dispatched after successful persistence via the Unit of Work.

### Unit of Work Integration

```csharp
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;
    private readonly IMediator _mediator;

    public async Task CommitChanges(CancellationToken cancellationToken = default)
    {
        // Collect domain events before saving
        var domainEvents = _dbContext.ChangeTracker
            .Entries<BaseEntity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        // Save changes
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Dispatch events after successful save
        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }

        // Clear events from entities
        foreach (var entry in _dbContext.ChangeTracker.Entries<BaseEntity>())
        {
            entry.Entity.ClearDomainEvents();
        }
    }
}
```

## Key Principles

### 1. Events Are Immutable Records

Use sealed records for immutability:

```csharp
public sealed record OrderShipped : IDomainEvent
{
    public required Guid OrderId { get; init; }
    public required string TrackingNumber { get; init; }
    public required DateTimeOffset ShippedAt { get; init; }
}
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
public sealed record OrderSubmitted : IDomainEvent
{
    public required Guid OrderId { get; init; }
    public required Guid CustomerId { get; init; }
    public required decimal TotalAmount { get; init; }
    public required int ItemCount { get; init; }
}

// Avoid - requiring database lookups in handlers
public sealed record OrderSubmitted : IDomainEvent
{
    public required Guid OrderId { get; init; }  // Handlers must query for details
}
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

### 6. Use Required Properties

Ensure all event properties are initialized:

```csharp
public sealed record CustomerRegistered : IDomainEvent
{
    public required Guid CustomerId { get; init; }
    public required string Email { get; init; }
    public required DateTimeOffset RegisteredAt { get; init; }
}
```

## Common Event Patterns

### Created Event with Full Entity

For new entity creation, include the full entity:

```csharp
public sealed record CustomerCreated : IDomainEvent
{
    public required Customer Customer { get; init; }
}
```

### Updated Event with Changed Fields

For updates, include only what's needed:

```csharp
// Simple - just the ID
public sealed record CustomerUpdated : IDomainEvent
{
    public required Guid Id { get; init; }
}

// Detailed - include changed fields
public sealed record CustomerAddressUpdated : IDomainEvent
{
    public required Guid CustomerId { get; init; }
    public required Address OldAddress { get; init; }
    public required Address NewAddress { get; init; }
}
```

### Deleted Event

For soft deletes:

```csharp
public sealed record CustomerDeleted : IDomainEvent
{
    public required Guid Id { get; init; }
    public required string DeletedBy { get; init; }
    public required DateTimeOffset DeletedAt { get; init; }
}
```

### Business Action Events

For domain-specific operations:

```csharp
public sealed record SubscriptionRenewed : IDomainEvent
{
    public required Guid SubscriptionId { get; init; }
    public required Guid CustomerId { get; init; }
    public required DateOnly NewExpirationDate { get; init; }
    public required decimal AmountCharged { get; init; }
}

public sealed record InventoryReplenished : IDomainEvent
{
    public required Guid ProductId { get; init; }
    public required int QuantityAdded { get; init; }
    public required int NewTotalQuantity { get; init; }
}
```

## Anti-Patterns to Avoid

### Don't Dispatch Events Before Persistence

```csharp
// Bad - event dispatched before save
public async Task Handle(Command request, CancellationToken ct)
{
    var order = Order.Create(...);
    await _mediator.Publish(new OrderCreated { Order = order });  // Too early!
    await _dbContext.Orders.AddAsync(order);
    await _unitOfWork.CommitChanges(ct);  // What if this fails?
}

// Good - events queued and dispatched by UnitOfWork
public async Task Handle(Command request, CancellationToken ct)
{
    var order = Order.Create(...);  // Queues OrderCreated event
    await _dbContext.Orders.AddAsync(order);
    await _unitOfWork.CommitChanges(ct);  // Dispatches events after save
}
```

### Don't Create Events Outside Entities

```csharp
// Bad - event created in handler
public async Task Handle(Command request, CancellationToken ct)
{
    var order = await _dbContext.Orders.GetById(request.Id);
    order.Status = OrderStatus.Submitted();  // Direct property mutation!
    await _mediator.Publish(new OrderSubmitted { ... });  // Event not from entity
}

// Good - entity creates its own events
public async Task Handle(Command request, CancellationToken ct)
{
    var order = await _dbContext.Orders.GetById(request.Id);
    order.Submit();  // Entity queues OrderSubmitted event
    await _unitOfWork.CommitChanges(ct);
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
    QueueDomainEvent(new OrderSubmitted { ... });
    return this;
}
```
