using System.Globalization;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FullstackTemplate.Bff.Authentication;

/// <summary>
/// <c>Auth:DefaultAccessTokenLifetime</c>: the lifetime that the BFF uses for an access token when the token
/// response has no <c>expires_in</c> (RFC 6749 section 5.1 only recommends it). With no value, such a token does
/// not expire, which is the default of <c>Duende.AccessTokenManagement</c>, and the BFF never refreshes it. The
/// OpenID conformance suite sends no <c>expires_in</c>, so the conformance job sets a short value to get a refresh.
/// It applies to the code exchange and to each refresh.
/// </summary>
public static class DefaultAccessTokenLifetime
{
    public const string Key = "Auth:DefaultAccessTokenLifetime";

    /// <summary>
    /// The code exchange: sets <c>expires_in</c> on a token response that has none, before the OIDC handler saves
    /// the tokens.
    /// </summary>
    public static void ApplyTo(OpenIdConnectMessage tokenResponse, IConfiguration configuration)
    {
        if (string.IsNullOrEmpty(tokenResponse.ExpiresIn) && configuration.GetValue<TimeSpan?>(Key) is { } lifetime)
            tokenResponse.ExpiresIn = ((long)lifetime.TotalSeconds).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The refresh: wraps the token endpoint of token management. Call it after
    /// <see cref="RefreshFailureMetrics.Register"/>, which wraps the same registration.
    /// </summary>
    public static void AddToRefresh(IServiceCollection services)
    {
        var registered = services.Single(d => d.ServiceType == typeof(IOpenIdConnectUserTokenEndpoint));
        Func<IServiceProvider, object> inner = registered.ImplementationFactory
            ?? (provider => ActivatorUtilities.CreateInstance(provider, registered.ImplementationType
                ?? throw new InvalidOperationException("Token management registered its token endpoint with no implementation type.")));
        services.Remove(registered);
        services.Add(ServiceDescriptor.Describe(
            typeof(IOpenIdConnectUserTokenEndpoint),
            provider => new RefreshEndpoint(
                (IOpenIdConnectUserTokenEndpoint)inner(provider),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<IConfiguration>()),
            registered.Lifetime));
    }

    /// <summary>
    /// Token management gives <see cref="DateTimeOffset.MaxValue"/> as the expiry of a refreshed token when the
    /// response has no <c>expires_in</c>. This gives it the default lifetime instead, when there is one.
    /// </summary>
    private sealed class RefreshEndpoint(IOpenIdConnectUserTokenEndpoint inner, TimeProvider timeProvider, IConfiguration configuration)
        : IOpenIdConnectUserTokenEndpoint
    {
        public async Task<TokenResult<UserToken>> RefreshAccessTokenAsync(UserRefreshToken userToken,
            UserTokenRequestParameters parameters, CancellationToken ct = default)
        {
            var result = await inner.RefreshAccessTokenAsync(userToken, parameters, ct);
            return result.WasSuccessful(out var token) && token.Expiration == DateTimeOffset.MaxValue
                   && configuration.GetValue<TimeSpan?>(Key) is { } lifetime
                ? token with { Expiration = timeProvider.GetUtcNow() + lifetime }
                : result;
        }

        public Task RevokeRefreshTokenAsync(UserRefreshToken userToken, UserTokenRequestParameters parameters,
            CancellationToken ct = default) =>
            inner.RevokeRefreshTokenAsync(userToken, parameters, ct);
    }
}
