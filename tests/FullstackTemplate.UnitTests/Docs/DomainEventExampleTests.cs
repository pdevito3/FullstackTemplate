namespace FullstackTemplate.UnitTests.Docs;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using MediatR;
using Shouldly;

// The example of docs/backend/domain-events.md.

public sealed record InspectionFailed(string InspectionId, string VehicleId) : DomainEvent;

public sealed class Inspection : BaseEntity
{
    public const string IdPrefix = "insp";

    private Inspection(string vehicleId) : base(IdPrefix) => VehicleId = vehicleId;

    public string VehicleId { get; private set; }
    public bool Failed { get; private set; }
    public string? Finding { get; private set; }

    public static Inspection Create(string vehicleId) => new(vehicleId);

    public Inspection Fail(string finding)
    {
        Failed = true;
        Finding = finding;
        QueueDomainEvent(new InspectionFailed(Id, VehicleId));
        return this;
    }
}

public sealed class Vehicle : BaseEntity
{
    public const string IdPrefix = "veh";

    private Vehicle() : base(IdPrefix) { }

    public bool InService { get; private set; } = true;

    public static Vehicle Create() => new();

    public Vehicle TakeOutOfService()
    {
        InService = false;
        return this;
    }
}

public static class FailInspection
{
    public sealed record Command(string Id, string Finding) : IRequest;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command>
    {
        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            var inspection = await dbContext.Set<Inspection>().GetById(request.Id, cancellationToken);

            inspection.Fail(request.Finding);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>
/// A vehicle that failed an inspection must not stay in service.
/// </summary>
public sealed class TakeVehicleOutOfServiceWhenInspectionFails : IDomainEventHandler<InspectionFailed>
{
    public async Task Handle(InspectionFailed domainEvent, AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var vehicle = await dbContext.Set<Vehicle>()
            .TagWithOperationCallSite("vehicles.out_of_service.load")
            .GetById(domainEvent.VehicleId, cancellationToken);

        vehicle.TakeOutOfService();
    }
}

public class DomainEventExampleTests
{
    [Test]
    public void a_failed_inspection_queues_the_event_with_the_ids_that_the_handler_needs()
    {
        // Arrange
        var vehicle = Vehicle.Create();
        var inspection = Inspection.Create(vehicle.Id);

        // Act
        inspection.Fail("The brakes do not hold.");

        // Assert
        inspection.DomainEvents.ShouldHaveSingleItem().ShouldBe(new InspectionFailed(inspection.Id, vehicle.Id));
    }
}
