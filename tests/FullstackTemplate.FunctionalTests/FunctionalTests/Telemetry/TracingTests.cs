namespace FullstackTemplate.FunctionalTests.FunctionalTests.Telemetry;

using System.Diagnostics;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Telemetry = FullstackTemplate.Server.Resources.Telemetry;

// These tests count the spans of the whole app, so no other test can run at the same time.
[Collection(nameof(TracingTests))]
public class TracingTests : TestBase
{
    private const string NpgsqlSource = "Npgsql";

    [Fact]
    public void startup_migration_exports_one_root_with_the_database_work_under_it()
    {
        // Assert
        var root = AllExportedSpans().Where(a => a.DisplayName == "database.migrate").ShouldHaveSingleItem();
        root.ParentSpanId.ShouldBe(default);
        root.GetTagItem("app.db_context").ShouldBe("AppDbContext");

        AllExportedSpans()
            .Where(a => a.Source.Name == NpgsqlSource && a.TraceId == root.TraceId)
            .ShouldNotBeEmpty();
    }

    [Fact]
    public async Task feature_slice_span_parents_the_tagged_query_span()
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant);
        await AddAsync(tenant);

        // Act
        await SendAsync(new GetTenant.Query(tenant.Id));

        // Assert
        var slice = ExportedSpans().Where(a => a.DisplayName == "GetTenant.Query").ShouldHaveSingleItem();
        var query = ExportedSpans().Where(a => a.DisplayName == "SQL: tenants.get_by_id").ShouldHaveSingleItem();
        query.ParentSpanId.ShouldBe(slice.SpanId);
        query.GetTagItem("db.operation.name").ShouldBe("tenants.get_by_id");
        query.GetTagItem("code.function").ShouldBe("Handle");
        (query.GetTagItem("code.file.path") as string).ShouldNotBeNull().ShouldEndWith("GetTenant.cs");
        query.GetTagItem("code.line.number").ShouldNotBeNull();
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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
}

[CollectionDefinition(nameof(TracingTests), DisableParallelization = true)]
public class TracingTestsCollection;
