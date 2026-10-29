namespace FullstackTemplate.UnitTests.Examples.Venues;

using FullstackTemplate.Server.Examples.Venues;
using FullstackTemplate.Server.Examples.Venues.DomainEvents;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using Shouldly;

public class VenueTests
{
    [Test]
    public void a_new_venue_is_in_the_tenant_of_its_parent_with_its_time_zone()
    {
        // Act
        TestGraph.Start(out var seed).AddVenue(out var venue, v => v.TimeZone = "Pacific/Tongatapu");

        // Assert
        venue.TenantId.ShouldBe(seed.Tenant.Id);
        venue.TimeZone.Value.ShouldBe("Pacific/Tongatapu");
        venue.DomainEvents.ShouldBe([new VenueCreated(venue.Id)]);
    }

    public static IEnumerable<Func<(Action<VenueInput> Customize, string Field, string Message)>> InvalidInputs()
    {
        yield return () => (v => v.Name = "", "name", "Please provide a venue name.");
        yield return () => (v => v.Name = new string('a', Venue.NameMaxLength + 1), "name",
            $"A venue name can have no more than {Venue.NameMaxLength} characters.");
        yield return () => (v => v.Name = "a\0b", "name", "A venue name has a character that is not allowed.");
        yield return () => (v => v.TimeZone = "Mars/Olympus", "timeZone",
            "'Mars/Olympus' is not a time zone that the server knows. Use an IANA ID such as America/Chicago.");
    }

    [Test]
    [MethodDataSource(nameof(InvalidInputs))]
    public void a_venue_refuses_an_invalid_value(Action<VenueInput> customize, string field, string message)
    {
        // Arrange
        var graph = TestGraph.Start(out _);

        // Act
        var act = () => graph.AddVenue(out _, customize);

        // Assert
        var refusal = act.ShouldThrow<Exception>().GetBaseException().ShouldBeOfType<ValidationException>();
        refusal.Property.ShouldBe(field);
        refusal.Message.ShouldBe(message);
    }
}
