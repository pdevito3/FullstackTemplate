namespace FullstackTemplate.FunctionalTests.FunctionalTests.Telemetry;

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Tenants.DomainEvents;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;
using TestUtilities;
using WebMotions.Fake.Authentication.JwtBearer;

public class DomainEventTracingTests : TestBase
{
    [Test]
    public async Task handler_span_is_exported_in_the_trace_of_the_request_under_the_feature_span()
    {
        // Arrange
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = "Admin");
        await InsertAsync(tenant);
        await InsertAsync(admin);
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = admin.Identifier });
        var traceId = ActivityTraceId.CreateRandom();
        using var request = new HttpRequestMessage(HttpMethod.Put, ApiRoutes.Tenants.Put(tenant.Id))
        {
            Content = JsonContent.Create(new { name = $"renamed-{Guid.NewGuid()}" }),
        };
        request.Headers.Add("traceparent", $"00-{traceId}-{ActivitySpanId.CreateRandom()}-01");

        // Act
        var response = await FactoryClient.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var spans = ExportedSpans().Where(a => a.TraceId == traceId).ToList();
        var feature = spans.Where(a => a.DisplayName == "UpdateTenant.Command").ShouldHaveSingleItem();
        var handler = spans.Where(a => a.DisplayName == "domain_event.handle").ShouldHaveSingleItem();
        handler.ParentSpanId.ShouldBe(feature.SpanId);
        handler.GetTagItem("app.domain_event.type").ShouldBe(nameof(TenantUpdated));
        handler.GetTagItem("app.domain_event.handler").ShouldBe(nameof(NoOpTenantUpdatedHandler));
    }
}
