namespace FullstackTemplate.UnitTests.Architecture;

using FullstackTemplate.ArchitectureFixtures;
using Shouldly;

/// <summary>
/// The Server is one project, and an adapter lives in the folder of its feature, next to the core. So no project
/// reference and no folder keeps the transports out of the core. <see cref="BoundaryScanner"/> enforces these rules
/// by type instead. The rules run on the Server, which must pass, and on <c>FullstackTemplate.ArchitectureFixtures</c>,
/// which has one violation for each rule and each place that the scan reads, so a broken scan cannot pass with no
/// results.
/// </summary>
public class EntrypointBoundaryTests
{
    /// <summary>
    /// The host wiring of the Server, outside <c>Program</c>. <c>HostExtensions</c> adds OpenTelemetry, Serilog, and
    /// the health checks of the host, and it maps the health endpoints. So it uses <c>HttpContext</c>,
    /// <c>PathString</c>, and the routing types, the same as <c>Program</c>.
    /// </summary>
    private static readonly string[] ServerCompositionRoot =
    [
        "FullstackTemplate.Server.Resources.Extensions.HostExtensions",
    ];

    /// <summary>
    /// The transport types that the core of the Server can use. <c>ConfigureServices</c> gets a
    /// <c>WebApplicationBuilder</c>, but that type is in <c>Microsoft.AspNetCore.Builder</c>, so it needs no entry.
    /// </summary>
    private static readonly string[] ServerAllowedTransportTypes = [];

    private static readonly Lazy<BoundaryScanner> Server = new(() => new BoundaryScanner(
        typeof(FullstackTemplate.Server.Resources.PagedList).Assembly,
        "FullstackTemplate.Server",
        ServerCompositionRoot,
        ServerAllowedTransportTypes));

    private static readonly Lazy<BoundaryScanner> Fixtures = new(() => new BoundaryScanner(
        typeof(FixtureMarker).Assembly,
        FixtureMarker.RootNamespace,
        allowedTransportTypes: [FixtureMarker.AllowedTransportType]));

    [Test]
    public void core_does_not_use_a_transport_library()
    {
        Server.Value.CoreThatUsesATransport().ShouldBeEmpty(
            "Move the transport code to an adapter in the feature folder, or map the transport type to a core type in the adapter.");
    }

    [Test]
    public void core_does_not_use_an_adapter_or_the_shared_plumbing()
    {
        Server.Value.CoreThatUsesAnEntrypoint().ShouldBeEmpty(
            "A feature, a service, or the database must not depend on an entrypoint. Move the shared code to the core.");
    }

    [Test]
    public void adapters_do_not_use_the_database()
    {
        Server.Value.AdaptersThatUseTheDatabase().ShouldBeEmpty(
            "An adapter sends a MediatR request. The handler reads and writes the database.");
    }

    [Test]
    public void each_kind_of_server_type_is_found()
    {
        var adapters = Server.Value.AdapterNames();
        adapters.ShouldContain("FullstackTemplate.Server.Domain.Tenants.Features.AddTenantEndpoint");
        adapters.ShouldContain("FullstackTemplate.Server.Domain.Tenants.Features.AddTenantTool");
        adapters.ShouldContain("FullstackTemplate.Server.Domain.Tenants.Grpc.V1.TenantsGrpcService");
        adapters.ShouldContain("FullstackTemplate.Server.Domain.Tenants.Grpc.V1.TenantGrpcMappings");
        Server.Value.GrpcContractNamespaces().ShouldBe(["FullstackTemplate.Server.Domain.Tenants.Grpc.V1"]);

        var core = Server.Value.CoreNames();
        core.ShouldContain("FullstackTemplate.Server.Databases.AppDbContext");
        core.ShouldContain("FullstackTemplate.Server.Domain.Tenants.Features.AddTenant");
        core.ShouldContain("FullstackTemplate.Server.Services.CurrentUserService");
        core.ShouldNotContain("FullstackTemplate.Server.Domain.Tenants.Grpc.V1.TenantsReflection");
    }

    [Test]
    public void rule_1_finds_each_kind_of_transport_use()
    {
        Fixtures.Value.CoreThatUsesATransport().ShouldBe(
        [
            "FullstackTemplate.ArchitectureFixtures.Core.CoreWithAsyncTransportUse",
            "FullstackTemplate.ArchitectureFixtures.Core.CoreWithGenericConstraint`1",
            "FullstackTemplate.ArchitectureFixtures.Core.CoreWithHttpResult",
            "FullstackTemplate.ArchitectureFixtures.Core.CoreWithMethodGenericConstraint",
            "FullstackTemplate.ArchitectureFixtures.Core.CoreWithNamedAttributeArgument",
            "FullstackTemplate.ArchitectureFixtures.Core.CoreWithReturnAttribute",
        ]);
    }

    [Test]
    public void rule_2_finds_core_that_uses_an_entrypoint()
    {
        Fixtures.Value.CoreThatUsesAnEntrypoint().ShouldBe(
        [
            "FullstackTemplate.ArchitectureFixtures.Core.CoreWithAdapter",
            "FullstackTemplate.ArchitectureFixtures.Core.CoreWithGrpcContract",
            "FullstackTemplate.ArchitectureFixtures.Core.CoreWithSharedPlumbing",
        ]);
    }

    [Test]
    public void rule_3_finds_adapters_that_use_the_database()
    {
        Fixtures.Value.AdaptersThatUseTheDatabase().ShouldBe(
        [
            "FullstackTemplate.ArchitectureFixtures.Adapters.EndpointWithDatabase",
            "FullstackTemplate.ArchitectureFixtures.Adapters.GrpcServiceWithDatabase",
            "FullstackTemplate.ArchitectureFixtures.Adapters.ToolWithDatabase",
            "FullstackTemplate.ArchitectureFixtures.Contract.Grpc.V1.HandWrittenGrpcMappings",
            "FullstackTemplate.ArchitectureFixtures.Resources.Entrypoints.PlumbingWithDatabase",
        ]);
    }

    [Test]
    public void the_fixtures_sort_each_kind_of_type()
    {
        var adapters = Fixtures.Value.AdapterNames();
        adapters.ShouldContain("FullstackTemplate.ArchitectureFixtures.Adapters.CleanEndpoint");
        adapters.ShouldContain("FullstackTemplate.ArchitectureFixtures.Contract.Grpc.V1.HandWrittenGrpcMappings");
        adapters.ShouldNotContain("FullstackTemplate.ArchitectureFixtures.Contract.Grpc.V1.GeneratedMessage");
        Fixtures.Value.GrpcContractNamespaces().ShouldBe(["FullstackTemplate.ArchitectureFixtures.Contract.Grpc.V1"]);

        // A clean core type, and a core type that uses only the allowed transport type, are not in rule 1
        var core = Fixtures.Value.CoreNames();
        core.ShouldContain("FullstackTemplate.ArchitectureFixtures.Core.CleanCore");
        core.ShouldContain("FullstackTemplate.ArchitectureFixtures.Core.CoreWithAllowedTransportType");
    }
}
