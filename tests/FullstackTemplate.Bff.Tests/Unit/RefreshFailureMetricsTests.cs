namespace FullstackTemplate.Bff.Tests.Unit;

using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using FullstackTemplate.Bff.Authentication;
using FullstackTemplate.Bff.Tests.Fakes;
using Shouldly;

// The listener sees the measurements of the whole process, so no other test may run at the same time
[NotInParallel]
public sealed class RefreshFailureMetricsTests : IDisposable
{
    private static readonly UserRefreshToken RefreshToken = new(Duende.AccessTokenManagement.RefreshToken.Parse("rt-1"), DPoPProofKey: null);

    private readonly MetricRecorder _metrics = new();

    public void Dispose() => _metrics.Dispose();

    [Test]
    [Arguments("invalid_grant", "permanent")]
    [Arguments("bff_invalid_refresh_id_token", "permanent")]
    [Arguments("server_error", "temporary")]
    [Arguments("temporarily_unavailable", "temporary")]
    [Arguments("Service Unavailable", "temporary")]
    public async Task a_failed_refresh_counts_with_its_kind(string error, string kind)
    {
        // Arrange
        var endpoint = new RefreshFailureMetrics(new StubTokenEndpoint(() => TokenResult.Failure(error)));

        // Act
        var result = await endpoint.RefreshAccessTokenAsync(RefreshToken, new UserTokenRequestParameters());

        // Assert
        result.WasSuccessful(out _, out _).ShouldBeFalse();
        _metrics.Sum("bff.refresh.failures").ShouldBe(1);
        _metrics.Sum("bff.refresh.failures", ("kind", kind)).ShouldBe(1);
    }

    [Test]
    public async Task an_exception_counts_as_temporary_and_still_throws()
    {
        // Arrange
        var endpoint = new RefreshFailureMetrics(new StubTokenEndpoint(() => throw new HttpRequestException("down")));

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(() => endpoint.RefreshAccessTokenAsync(RefreshToken, new UserTokenRequestParameters()));

        _metrics.Sum("bff.refresh.failures", ("kind", "temporary")).ShouldBe(1);
    }

    [Test]
    public async Task a_cancelled_request_is_no_failure()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var endpoint = new RefreshFailureMetrics(new StubTokenEndpoint(() => throw new OperationCanceledException()));

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() =>
            endpoint.RefreshAccessTokenAsync(RefreshToken, new UserTokenRequestParameters(), cancellation.Token));

        _metrics.Sum("bff.refresh.failures").ShouldBe(0);
    }

    private sealed class StubTokenEndpoint(Func<TokenResult<UserToken>> refresh) : IOpenIdConnectUserTokenEndpoint
    {
        public Task<TokenResult<UserToken>> RefreshAccessTokenAsync(UserRefreshToken userToken, UserTokenRequestParameters parameters,
            CancellationToken ct = default) => Task.FromResult(refresh());

        public Task RevokeRefreshTokenAsync(UserRefreshToken userToken, UserTokenRequestParameters parameters,
            CancellationToken ct = default) => Task.CompletedTask;
    }
}
