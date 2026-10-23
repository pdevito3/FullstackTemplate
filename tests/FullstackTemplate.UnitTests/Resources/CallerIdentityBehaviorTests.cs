namespace FullstackTemplate.UnitTests.Resources;

using System.Diagnostics;
using System.Security.Claims;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.Server.Services;
using Microsoft.Extensions.Options;
using NSubstitute;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Shouldly;

/// <summary>
/// The tenant query filter reads the identity synchronously. The behavior resolves it first, so the handler never
/// blocks a thread on the lookups.
/// </summary>
public sealed class CallerIdentityBehaviorTests
{
    private const string AppClientId = "app";
    private const string AppUser = "user-1";

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ITenantIdProvider _tenantIdProvider = Substitute.For<ITenantIdProvider>();
    private readonly CallerIdentityResolver _resolver;

    public CallerIdentityBehaviorTests()
    {
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserIdentifier.Returns(AppUser);
        _currentUserService.ClientId.Returns(AppClientId);
        _currentUserService.User.Returns(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", AppUser), new Claim("azp", AppClientId)], "TestAuth")));

        _resolver = new CallerIdentityResolver(
            _currentUserService,
            _tenantIdProvider,
            Options.Create(new TenancyOptions()),
            Options.Create(new FirstPartyClientOptions { ClientIds = new HashSet<string> { AppClientId } }),
            Substitute.For<IAgentDirectory>(),
            new SystemTenantAccess());
    }

    [Test]
    public async Task handler_runs_after_the_identity_is_resolved()
    {
        // Arrange
        var tenantId = EntityId.New(Tenant.IdPrefix);
        var lookup = new TaskCompletionSource<string?>();
        _tenantIdProvider.GetTenantIdAsync(AppUser, Arg.Any<CancellationToken>()).Returns(lookup.Task);
        var behavior = new CallerIdentityBehavior<GetTenant.Query, TenantDto>(_resolver);
        CallerIdentity? identityInHandler = null;

        // Act
        var handled = behavior.Handle(new GetTenant.Query("ten_1"), _ =>
        {
            var resolution = _resolver.GetAsync(CancellationToken.None);
            identityInHandler = resolution.IsCompletedSuccessfully ? resolution.Result : null;
            return Task.FromResult(new TenantDto());
        }, CancellationToken.None);
        var handlerRanBeforeTheLookup = identityInHandler is not null;
        lookup.SetResult(tenantId);
        await handled;

        // Assert
        handlerRanBeforeTheLookup.ShouldBeFalse();
        identityInHandler.ShouldBe(new CallerIdentity.User(AppUser, tenantId));
    }

    [Test]
    public async Task canceled_first_read_reaches_the_lookup_and_is_not_kept()
    {
        // Arrange
        var tenantId = EntityId.New(Tenant.IdPrefix);
        using var request = new CancellationTokenSource();
        await request.CancelAsync();
        _tenantIdProvider.GetTenantIdAsync(AppUser, Arg.Any<CancellationToken>()).Returns(tenantId);
        _tenantIdProvider.GetTenantIdAsync(AppUser, request.Token)
            .Returns(call => Task.FromCanceled<string?>(call.ArgAt<CancellationToken>(1)));

        // Act
        var firstRead = _resolver.GetAsync(request.Token);

        // Assert
        await Should.ThrowAsync<TaskCanceledException>(firstRead);
        _resolver.Current.ShouldBe(new CallerIdentity.User(AppUser, tenantId));
    }

    [Test]
    public async Task feature_span_and_handler_logs_get_the_ids_of_the_caller()
    {
        // Arrange
        var caller = new CallerIdentity.DelegatedAgent("agent_1", "agent-client", "user-1", "tenant_1");
        var behavior = BehaviorFor(caller);
        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();
        using var featureSpan = new Activity("GetTenant.Query").Start();

        // Act
        await behavior.Handle(new GetTenant.Query("ten_1"), _ =>
        {
            logger.Information("Read tenant {TenantId}", "ten_1");
            return Task.FromResult(new TenantDto());
        }, CancellationToken.None);

        // Assert
        featureSpan.GetTagItem("app.caller.kind").ShouldBe("delegated_agent");
        featureSpan.GetTagItem("app.caller.subject").ShouldBe("user-1");
        featureSpan.GetTagItem("app.caller.client_id").ShouldBe("agent-client");
        featureSpan.GetTagItem("app.caller.agent_id").ShouldBe("agent_1");
        featureSpan.GetTagItem("app.caller.tenant_id").ShouldBe("tenant_1");

        var properties = sink.Events.ShouldHaveSingleItem().Properties;
        Scalar(properties, "CallerKind").ShouldBe("delegated_agent");
        Scalar(properties, "CallerSubject").ShouldBe("user-1");
        Scalar(properties, "CallerClientId").ShouldBe("agent-client");
        Scalar(properties, "CallerAgentId").ShouldBe("agent_1");
        Scalar(properties, "CallerTenantId").ShouldBe("tenant_1");
    }

    [Test]
    public async Task caller_with_no_tenant_gets_no_tenant_tag()
    {
        // Arrange
        var behavior = BehaviorFor(new CallerIdentity.User("user-1", TenantId: null));
        using var featureSpan = new Activity("GetTenant.Query").Start();

        // Act
        await behavior.Handle(new GetTenant.Query("ten_1"), _ => Task.FromResult(new TenantDto()), CancellationToken.None);

        // Assert
        featureSpan.GetTagItem("app.caller.kind").ShouldBe("user");
        featureSpan.GetTagItem("app.caller.subject").ShouldBe("user-1");
        featureSpan.GetTagItem("app.caller.tenant_id").ShouldBeNull();
        featureSpan.GetTagItem("app.caller.client_id").ShouldBeNull();
    }

    private static CallerIdentityBehavior<GetTenant.Query, TenantDto> BehaviorFor(CallerIdentity caller)
    {
        var resolver = Substitute.For<ICallerIdentityResolver>();
        resolver.GetAsync(Arg.Any<CancellationToken>()).Returns(caller);
        return new CallerIdentityBehavior<GetTenant.Query, TenantDto>(resolver);
    }

    private static object? Scalar(IReadOnlyDictionary<string, LogEventPropertyValue> properties, string name) =>
        properties[name].ShouldBeOfType<ScalarValue>().Value;

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
