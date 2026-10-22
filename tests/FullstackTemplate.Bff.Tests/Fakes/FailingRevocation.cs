namespace FullstackTemplate.Bff.Tests.Fakes;

using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Wraps the token endpoint of token management. The first revocation throws an error that
/// <c>RefreshTokenRevocation</c> does not catch. The other calls go to the real token endpoint.
/// </summary>
public sealed class FailingRevocation(IOpenIdConnectUserTokenEndpoint inner, Func<bool> failThisRevocation)
    : IOpenIdConnectUserTokenEndpoint
{
    public Task<TokenResult<UserToken>> RefreshAccessTokenAsync(UserRefreshToken userToken,
        UserTokenRequestParameters parameters, CancellationToken ct = default) =>
        inner.RefreshAccessTokenAsync(userToken, parameters, ct);

    public Task RevokeRefreshTokenAsync(UserRefreshToken userToken, UserTokenRequestParameters parameters,
        CancellationToken ct = default) =>
        failThisRevocation()
            ? throw new InvalidOperationException("A test failed the first revocation.")
            : inner.RevokeRefreshTokenAsync(userToken, parameters, ct);

    /// <summary>
    /// Wraps the token endpoint that the app registered. Only the first revocation of the app fails.
    /// </summary>
    public static void Register(IServiceCollection services)
    {
        var registered = services.Last(d => d.ServiceType == typeof(IOpenIdConnectUserTokenEndpoint));
        var factory = registered.ImplementationFactory
            ?? throw new InvalidOperationException("The app registered its token endpoint with no factory.");

        services.Remove(registered);
        var failed = 0;
        services.Add(ServiceDescriptor.Describe(
            typeof(IOpenIdConnectUserTokenEndpoint),
            sp => new FailingRevocation((IOpenIdConnectUserTokenEndpoint)factory(sp), () => Interlocked.Exchange(ref failed, 1) == 0),
            registered.Lifetime));
    }
}
