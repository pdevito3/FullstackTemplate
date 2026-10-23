namespace FullstackTemplate.UnitTests.Resources;

using System.Security.Claims;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.Server.Services;
using Microsoft.Extensions.Options;
using NSubstitute;
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
}
