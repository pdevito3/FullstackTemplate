namespace FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;

using Bogus;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Examples.Venues;
using FullstackTemplate.Server.Examples.Venues.Models;
using FullstackTemplate.Server.Examples.Workshops;
using FullstackTemplate.Server.Examples.Workshops.Models;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;
using Fakes;

[GraphGraft]
public static class ExampleBlueprints
{
    private static readonly Faker Faker = new();

    public static readonly ChildInputBlueprint<TestSeed, Tenant, VenueInput, Venue> Venue = Blueprint
        .ChildFromInput<TestSeed, Tenant, VenueInput, Venue>(
            () => new(),
            (tenant, input, _) => Server.Examples.Venues.Venue.Create(new VenueForCreation
            {
                Id = input.Id,
                TenantId = tenant.Id,
                Name = input.Name!,
                TimeZone = input.TimeZone!,
            }))
        .Derive(i => i.Name, (_, _) => VenueRules.Name(Faker))
        .Derive(i => i.TimeZone, (_, _) => VenueRules.TimeZone(Faker));

    public static readonly ChildInputBlueprint<TestSeed, Venue, WorkshopInput, Workshop> Workshop = Blueprint
        .ChildFromInput<TestSeed, Venue, WorkshopInput, Workshop>(
            () => new(),
            (venue, input, _) => Server.Examples.Workshops.Workshop.Create(venue, new WorkshopForCreation
            {
                Id = input.Id,
                Title = input.Title!,
                StartsAt = input.StartsAt!.Value,
                Capacity = input.Capacity!.Value,
                OrganizerId = input.OrganizerId,
            }))
        .Derive(i => i.Title, (_, _) => WorkshopRules.Title(Faker))
        .Derive(i => i.StartsAt, (_, ctx) => WorkshopRules.StartsAt(ctx.Parent.TimeZone, ctx.Now))
        .Derive(i => i.Capacity, (_, _) => WorkshopRules.Capacity(Faker));
}
