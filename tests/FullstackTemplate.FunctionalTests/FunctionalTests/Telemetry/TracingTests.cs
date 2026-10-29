namespace FullstackTemplate.FunctionalTests.FunctionalTests.Telemetry;

using System.Diagnostics;
using System.Net;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;
using Shouldly;
using TestUtilities;
using Telemetry = FullstackTemplate.Server.Resources.Telemetry;

// These tests count the spans of the whole app, so no other test can run at the same time.
[NotInParallel]
public class TracingTests : TestBase
{
    private const string NpgsqlSource = "Npgsql";

    [Test]
    public void startup_migration_exports_one_root_with_the_database_work_under_it()
    {
        // Act & Assert
        var root = AllExportedSpans().Where(a => a.DisplayName == "database.migrate").ShouldHaveSingleItem();
        root.ParentSpanId.ShouldBe(default);
        root.GetTagItem("app.db_context").ShouldBe("AppDbContext");

        AllExportedSpans()
            .Where(a => a.Source.Name == NpgsqlSource && a.TraceId == root.TraceId)
            .ShouldNotBeEmpty();
    }

    [Test]
    public void startup_migration_on_an_empty_database_logs_no_error()
    {
        // Act & Assert
        var root = AllExportedSpans().Where(a => a.DisplayName == "database.migrate").ShouldHaveSingleItem();
        TestingWebApplicationFactory.Instance.CapturedLogs.InTrace(root.TraceId)
            .Where(e => e.Level >= LogEventLevel.Error)
            .Select(e => e.MessageTemplate.Text)
            .ShouldBeEmpty();
    }

    [Test]
    public async Task request_span_keeps_the_query_keys_and_hides_the_values()
    {
        // Arrange
        var key = $"probe_{Guid.NewGuid():N}";

        // Act
        await FactoryClient.GetAsync($"{ApiRoutes.Users.GetList}?filters=email%20%40%3D*%20%22jane%22&{key}=jane");

        // Assert
        var request = await WaitForSpanAsync(a =>
            a.Kind == ActivityKind.Server && (a.GetTagItem("url.query") as string ?? "").Contains(key, StringComparison.Ordinal));
        request.GetTagItem("url.query").ShouldBe($"?filters=Redacted&{key}=Redacted");
    }

    [Test]
    public async Task paged_list_names_its_count_query_apart_from_its_page_query()
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant);
        await AddAsync(tenant);
        using var parent = Telemetry.Source.StartActivity("test.parent").ShouldNotBeNull();

        // Act
        await SendAsync(new GetTenantList.Query(new TenantParametersDto()));
        parent.Stop();

        // Assert
        var queries = ExportedSpans()
            .Where(a => a.TraceId == parent.TraceId && a.DisplayName.StartsWith("SQL: tenants.list", StringComparison.Ordinal))
            .ToList();
        queries.Select(a => a.DisplayName).ShouldBe(["SQL: tenants.list.count", "SQL: tenants.list"], ignoreOrder: true);
        queries.Select(a => a.GetTagItem("db.operation.name"))
            .ShouldBe(["tenants.list.count", "tenants.list"], ignoreOrder: true);
    }

    [Test]
    [Arguments("name == \"VALUE\"")]
    [Arguments("name @=* \"VALUE\"")]
    [Arguments("name ^^ [\"VALUE\", \"other\"]")]
    public async Task list_filter_value_is_not_in_the_sql_text_of_the_query_span(string filter)
    {
        // Arrange
        var value = $"probe{Guid.NewGuid():N}";
        using var parent = Telemetry.Source.StartActivity("test.parent").ShouldNotBeNull();

        // Act
        await SendAsync(new GetTenantList.Query(new TenantParametersDto { Filters = filter.Replace("VALUE", value, StringComparison.Ordinal) }));
        parent.Stop();

        // Assert
        var sqlTexts = ExportedSpans()
            .Where(a => a.TraceId == parent.TraceId && a.DisplayName.StartsWith("SQL: tenants.list", StringComparison.Ordinal))
            .Select(a => a.GetTagItem("db.query.text") as string)
            .ToList();
        sqlTexts.Count.ShouldBe(2);
        sqlTexts.ShouldAllBe(text => text != null && !text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task feature_slice_span_parents_the_tagged_query_span()
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant);
        await AddAsync(tenant);
        // Tests in other classes run at the same time and can also send GetTenant. The trace of this
        // parent keeps their spans out of the assertions.
        using var parent = Telemetry.Source.StartActivity("test.parent").ShouldNotBeNull();

        // Act
        await SendAsync(new GetTenant.Query(tenant.Id));
        parent.Stop();

        // Assert
        var spans = ExportedSpans().Where(a => a.TraceId == parent.TraceId).ToList();
        var slice = spans.Where(a => a.DisplayName == "GetTenant.Query").ShouldHaveSingleItem();
        var query = spans.Where(a => a.DisplayName == "SQL: tenants.get_by_id").ShouldHaveSingleItem();
        query.ParentSpanId.ShouldBe(slice.SpanId);
        query.GetTagItem("db.operation.name").ShouldBe("tenants.get_by_id");
        query.GetTagItem("code.function").ShouldBe("Handle");
        (query.GetTagItem("code.file.path") as string).ShouldNotBeNull().ShouldEndWith("GetTenant.cs");
        query.GetTagItem("code.line.number").ShouldNotBeNull();
    }

    [Test]
    public async Task feature_slice_span_has_the_caller()
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant);
        await AddAsync(tenant);
        using var parent = Telemetry.Source.StartActivity("test.parent").ShouldNotBeNull();

        // Act
        await SendAsync(new GetTenant.Query(tenant.Id));
        parent.Stop();

        // Assert
        var slice = ExportedSpans()
            .Where(a => a.TraceId == parent.TraceId && a.DisplayName == "GetTenant.Query")
            .ShouldHaveSingleItem();
        slice.GetTagItem("app.caller.kind").ShouldBe("system");
    }

    [Test]
    public async Task a_request_that_fails_validation_has_the_caller_on_its_span()
    {
        // Arrange
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var user);
        await InsertAsync(tenant);
        await InsertAsync(user);
        FactoryClient.SignInAsAppUser(user.Identifier);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.UpdateRole(user.Id), new { });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var slice = ExportedSpans()
            .Where(a => a.DisplayName == "UpdateUserRole.Command"
                && a.GetTagItem("app.caller.subject") as string == user.Identifier)
            .ShouldHaveSingleItem();
        slice.GetTagItem("app.caller.kind").ShouldBe("user");
        slice.GetTagItem("app.caller.tenant_id").ShouldBe(tenant.Id);
    }

    [Test]
    public async Task untagged_query_keeps_the_default_span_name()
    {
        // Arrange
        using var parent = Telemetry.Source.StartActivity("test.parent").ShouldNotBeNull();

        // Act
        await ExecuteDbContextAsync(db => db.Tenants.CountAsync());
        parent.Stop();

        // Assert
        var query = ExportedSpans().Where(a =>
            a.Source.Name == NpgsqlSource && a.ParentSpanId == parent.SpanId).ShouldHaveSingleItem();
        query.DisplayName.ShouldNotStartWith("SQL: ");
        query.GetTagItem("app.db.call_site").ShouldBeNull();
    }

    [Test]
    public async Task query_without_an_ambient_activity_is_not_exported()
    {
        // Arrange
        Activity.Current = null;
        var before = ExportedSpans().Count(a => a.Source.Name == NpgsqlSource);

        // Act
        await ExecuteDbContextAsync(db => db.Tenants.CountAsync());

        // Assert
        ExportedSpans().Count(a => a.Source.Name == NpgsqlSource).ShouldBe(before);
    }

    [Test]
    public async Task query_under_a_filtered_out_activity_is_not_exported()
    {
        // Arrange - this is what the ASP.NET Core instrumentation filter does to a /health request
        using var filtered = Telemetry.Source.StartActivity("test.filtered").ShouldNotBeNull();
        filtered.IsAllDataRequested = false;

        // Act
        await ExecuteDbContextAsync(db => db.Tenants.CountAsync());

        // Assert
        ExportedSpans().ShouldNotContain(a => a.TraceId == filtered.TraceId);
    }

    // The server ends the request span after it sends the response, so the span can export a moment after the client
    // has the response
    private async Task<Activity> WaitForSpanAsync(Func<Activity, bool> match)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            var found = ExportedSpans().Where(match).ToList();
            if (found.Count > 0)
                return found.ShouldHaveSingleItem();
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("No matching span.");
            await Task.Delay(20);
        }
    }
}
