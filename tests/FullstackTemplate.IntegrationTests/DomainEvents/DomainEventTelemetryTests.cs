namespace FullstackTemplate.IntegrationTests.DomainEvents;

using System.Diagnostics;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants.DomainEvents;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

public class DomainEventTelemetryTests : TestBase
{
    private const string FeatureSpan = "UpdateTenant.Command";
    private const string HandlerSpan = "domain_event.handle";

    [Test]
    public async Task handler_span_is_a_child_of_the_feature_span_and_names_the_types()
    {
        // Arrange
        using var recorder = new SpanRecorder();

        // Act
        await RenameTenantAsync(() => Task.CompletedTask);

        // Assert
        var feature = recorder.Spans.Where(a => a.DisplayName == FeatureSpan).ShouldHaveSingleItem();
        var handler = recorder.Spans.Where(a => a.DisplayName == HandlerSpan).ShouldHaveSingleItem();
        handler.ParentSpanId.ShouldBe(feature.SpanId);
        handler.GetTagItem("app.domain_event.type").ShouldBe(nameof(TenantUpdated));
        handler.GetTagItem("app.domain_event.handler").ShouldBe(nameof(TenantUpdatedProbe));
        handler.Status.ShouldBe(ActivityStatusCode.Unset);
    }

    [Test]
    public async Task server_error_in_a_handler_marks_the_handler_span_failed()
    {
        // Arrange
        using var recorder = new SpanRecorder();

        // Act
        var act = () => RenameTenantAsync(() => throw new InvalidOperationException("The handler failed."));

        // Assert
        await act.ShouldThrowAsync<InvalidOperationException>();
        var handler = recorder.Spans.Where(a => a.DisplayName == HandlerSpan).ShouldHaveSingleItem();
        handler.Status.ShouldBe(ActivityStatusCode.Error);
        handler.GetTagItem("error.type").ShouldBe(typeof(InvalidOperationException).FullName);
        handler.Events.ShouldContain(e => e.Name == "exception");
    }

    [Test]
    public async Task client_error_in_a_handler_does_not_mark_the_handler_span_failed()
    {
        // Arrange
        using var recorder = new SpanRecorder();

        // Act
        var act = () => RenameTenantAsync(() => throw new ValidationException("name", "The handler refused the name."));

        // Assert
        await act.ShouldThrowAsync<ValidationException>();
        var handler = recorder.Spans.Where(a => a.DisplayName == HandlerSpan).ShouldHaveSingleItem();
        handler.Status.ShouldBe(ActivityStatusCode.Unset);
        handler.GetTagItem("error.type").ShouldBe(typeof(ValidationException).FullName);
        handler.Events.ShouldNotContain(e => e.Name == "exception");
    }

    private static async Task RenameTenantAsync(Func<Task> handle)
    {
        await using var services = TestFixture.BuildServiceProvider(s =>
            s.AddScoped<IDomainEventHandler<TenantUpdated>>(_ => new TenantUpdatedProbe(handle)));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        await scope.SendAsync(new UpdateTenant.Command(scenario.Tenant.Id,
            new TenantForUpdateDto { Name = $"renamed-{Guid.NewGuid()}" }));
    }

    private sealed class TenantUpdatedProbe(Func<Task> handle) : IDomainEventHandler<TenantUpdated>
    {
        public Task Handle(TenantUpdated domainEvent, AppDbContext dbContext, CancellationToken cancellationToken)
            => handle();
    }
}
