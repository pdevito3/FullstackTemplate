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
        await using var rename = await TenantRename.ArrangeAsync(() => Task.CompletedTask);
        using var recorder = new SpanRecorder();

        // Act
        await rename.RunAsync();

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
        await using var rename = await TenantRename.ArrangeAsync(() => throw new InvalidOperationException("The handler failed."));
        using var recorder = new SpanRecorder();

        // Act
        var act = () => rename.RunAsync();

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
        await using var rename = await TenantRename.ArrangeAsync(() => throw new ValidationException("name", "The handler refused the name."));
        using var recorder = new SpanRecorder();

        // Act
        var act = () => rename.RunAsync();

        // Assert
        await act.ShouldThrowAsync<ValidationException>();
        var handler = recorder.Spans.Where(a => a.DisplayName == HandlerSpan).ShouldHaveSingleItem();
        handler.Status.ShouldBe(ActivityStatusCode.Unset);
        handler.GetTagItem("error.type").ShouldBe(typeof(ValidationException).FullName);
        handler.Events.ShouldNotContain(e => e.Name == "exception");
    }

    /// <summary>
    /// The rename of a tenant on a Server whose probe handles <see cref="TenantUpdated"/>. The save of the scenario runs
    /// handlers too, so start the recorder after <see cref="ArrangeAsync"/>.
    /// </summary>
    private sealed class TenantRename(ServiceProvider services, TestingServiceScope scope, TenantScenario scenario)
        : IAsyncDisposable
    {
        public static async Task<TenantRename> ArrangeAsync(Func<Task> handle)
        {
            var services = TestFixture.BuildServiceProvider(s =>
                s.AddScoped<IDomainEventHandler<TenantUpdated>>(_ => new TenantUpdatedProbe(handle)));
            var scope = new TestingServiceScope(services);
            return new TenantRename(services, scope, await scope.StartTenantScenarioAsync());
        }

        public Task<TenantDto> RunAsync() => scope.SendAsync(new UpdateTenant.Command(scenario.Tenant.Id,
            new TenantForUpdateDto { Name = $"renamed-{Guid.NewGuid()}", Version = scenario.Tenant.Version }));

        public ValueTask DisposeAsync() => services.DisposeAsync();
    }

    private sealed class TenantUpdatedProbe(Func<Task> handle) : IDomainEventHandler<TenantUpdated>
    {
        public Task Handle(TenantUpdated domainEvent, AppDbContext dbContext, CancellationToken cancellationToken)
            => handle();
    }
}
