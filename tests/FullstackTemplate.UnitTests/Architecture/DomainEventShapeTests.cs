namespace FullstackTemplate.UnitTests.Architecture;

using System.Reflection;
using FullstackTemplate.ArchitectureFixtures;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Users.DomainEvents;
using MediatR;
using Microsoft.Extensions.Options;
using Shouldly;

public class DomainEventShapeTests
{
    private static readonly Assembly Server = typeof(DomainEvent).Assembly;
    private static readonly Assembly Fixtures = typeof(FixtureMarker).Assembly;

    private static readonly Type[] AllowedHandlerDependencies = [typeof(TimeProvider), typeof(IOptions<>)];

    [Test]
    public void events_carry_no_entity()
    {
        EventsThatCarryAnEntity(Server).ShouldBeEmpty(
            "Put the ID of the entity in the event. The handler loads the entity from the dbContext of Handle.");
    }

    [Test]
    public void handlers_are_sealed()
    {
        HandlersThatAreNotSealed(Server).ShouldBeEmpty("Make the handler a sealed class.");
    }

    [Test]
    public void events_are_sealed()
    {
        EventsThatAreNotSealed(Server).ShouldBeEmpty("Make the event a sealed record.");
    }

    [Test]
    public void handlers_handle_a_sealed_event()
    {
        HandlersOfAnEventThatIsNotSealed(Server).ShouldBeEmpty(
            "The context runs the handlers of the runtime type of the event only. Handle a sealed event type, " +
            "not a base type.");
    }

    [Test]
    public void handlers_take_only_time_and_options()
    {
        HandlersWithAForbiddenDependency(Server).ShouldBeEmpty(
            "A handler takes only TimeProvider and IOptions<T> in its constructor. It changes entities through the " +
            "dbContext of Handle, and it adds an entity to ask for work outside the database.");
    }

    [Test]
    public void no_type_uses_mediatr_notifications()
    {
        TypesThatUseMediatRNotifications(Server).ShouldBeEmpty(
            "A MediatR notification runs outside the save transaction. Derive the event from DomainEvent, and " +
            "implement IDomainEventHandler<TEvent> in the handler.");
    }

    [Test]
    public void the_scan_finds_the_server_events()
    {
        EventTypes(Server).ShouldContain(typeof(UserCreated));
    }

    [Test]
    public void rule_1_finds_each_place_of_an_entity_in_an_event()
    {
        EventsThatCarryAnEntity(Fixtures).ShouldBe(
        [
            "FullstackTemplate.ArchitectureFixtures.DomainEvents.EventWithEntity",
            "FullstackTemplate.ArchitectureFixtures.DomainEvents.EventWithEntityArray",
            "FullstackTemplate.ArchitectureFixtures.DomainEvents.EventWithEntityField",
            "FullstackTemplate.ArchitectureFixtures.DomainEvents.EventWithEntityTypeArgument",
        ]);
    }

    [Test]
    public void rule_2_finds_a_handler_that_is_not_sealed()
    {
        HandlersThatAreNotSealed(Fixtures).ShouldBe(["FullstackTemplate.ArchitectureFixtures.DomainEvents.UnsealedHandler"]);
    }

    [Test]
    public void rule_3_finds_each_forbidden_constructor_parameter()
    {
        HandlersWithAForbiddenDependency(Fixtures).ShouldBe(
        [
            "FullstackTemplate.ArchitectureFixtures.DomainEvents.HandlerWithSecondConstructor",
            "FullstackTemplate.ArchitectureFixtures.DomainEvents.HandlerWithServiceProvider",
        ]);
    }

    [Test]
    public void rule_4_finds_each_mediatr_notification_type()
    {
        TypesThatUseMediatRNotifications(Fixtures).ShouldBe(
        [
            "FullstackTemplate.ArchitectureFixtures.DomainEvents.NotificationHandlerWithMediatR",
            "FullstackTemplate.ArchitectureFixtures.DomainEvents.NotificationWithMediatR",
        ]);
    }

    [Test]
    public void rule_5_finds_an_event_that_is_not_sealed()
    {
        EventsThatAreNotSealed(Fixtures).ShouldBe(["FullstackTemplate.ArchitectureFixtures.DomainEvents.UnsealedEvent"]);
    }

    [Test]
    public void rule_6_finds_a_handler_of_an_event_that_is_not_sealed()
    {
        HandlersOfAnEventThatIsNotSealed(Fixtures)
            .ShouldBe(["FullstackTemplate.ArchitectureFixtures.DomainEvents.HandlerOfAbstractEvent"]);
    }

    [Test]
    public void the_clean_fixtures_are_found_and_pass()
    {
        EventTypes(Fixtures).Select(t => t.Name).ShouldContain("CleanEvent");
        HandlerTypes(Fixtures).Select(t => t.Name).ShouldContain("CleanHandler");
        HandlerTypes(Fixtures).Select(t => t.Name).ShouldContain("CleanInternalHandler");
    }

    private static string[] EventsThatCarryAnEntity(Assembly assembly)
        => Names(EventTypes(assembly).Where(type => StoredTypes(type).SelectMany(Flatten).Any(IsEntity)));

    private static string[] HandlersThatAreNotSealed(Assembly assembly)
        => Names(HandlerTypes(assembly).Where(type => !type.IsSealed));

    private static string[] EventsThatAreNotSealed(Assembly assembly)
        => Names(EventTypes(assembly).Where(type => !type.IsSealed));

    private static string[] HandlersOfAnEventThatIsNotSealed(Assembly assembly)
        => Names(HandlerTypes(assembly).Where(type => HandledEventTypes(type).Any(eventType => !eventType.IsSealed)));

    private static string[] HandlersWithAForbiddenDependency(Assembly assembly)
        => Names(HandlerTypes(assembly).Where(type => type.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => !IsAllowedHandlerDependency(parameter.ParameterType))));

    private static string[] TypesThatUseMediatRNotifications(Assembly assembly)
        => Names(assembly.GetTypes().Where(type => type.GetInterfaces().Any(i =>
            i == typeof(INotification)
            || i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>))));

    private static IEnumerable<Type> EventTypes(Assembly assembly)
        => assembly.GetTypes().Where(type => !type.IsAbstract && type.IsAssignableTo(typeof(DomainEvent)));

    private static IEnumerable<Type> HandlerTypes(Assembly assembly)
        => assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false } && HandledEventTypes(type).Any());

    private static IEnumerable<Type> HandledEventTypes(Type handlerType)
        => handlerType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
            .Select(i => i.GetGenericArguments()[0]);

    private static IEnumerable<Type> StoredTypes(Type type)
    {
        const BindingFlags instanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        return type.GetProperties(instanceMembers).Select(property => property.PropertyType)
            .Concat(type.GetFields(instanceMembers).Select(field => field.FieldType));
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        var parts = type.HasElementType ? [type.GetElementType()!] : type.GetGenericArguments();
        foreach (var part in parts.SelectMany(Flatten))
            yield return part;
    }

    private static bool IsEntity(Type type) => type.IsAssignableTo(typeof(BaseEntity));

    private static bool IsAllowedHandlerDependency(Type type)
        => AllowedHandlerDependencies.Contains(type.IsGenericType ? type.GetGenericTypeDefinition() : type);

    private static string[] Names(IEnumerable<Type> types)
        => types.Select(type => type.FullName!).Order(StringComparer.Ordinal).ToArray();
}
