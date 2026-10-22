using Duende.AccessTokenManagement.OpenIdConnect;

namespace FullstackTemplate.Bff.Authentication;

/// <summary>
/// Wraps the registered <see cref="IOpenIdConnectUserTokenEndpoint"/> of token management. Each call wraps the
/// registration of the call before it, so the first call gives the innermost wrapper.
/// </summary>
public static class TokenEndpointDecorator
{
    public static void Decorate(IServiceCollection services,
        Func<IServiceProvider, IOpenIdConnectUserTokenEndpoint, IOpenIdConnectUserTokenEndpoint> wrap)
    {
        var registered = services.Single(d => d.ServiceType == typeof(IOpenIdConnectUserTokenEndpoint));
        Func<IServiceProvider, object> inner = registered.ImplementationFactory
            ?? (provider => ActivatorUtilities.CreateInstance(provider, registered.ImplementationType
                ?? throw new InvalidOperationException("Token management registered its token endpoint with no implementation type.")));
        services.Remove(registered);
        services.Add(ServiceDescriptor.Describe(
            typeof(IOpenIdConnectUserTokenEndpoint),
            provider => wrap(provider, (IOpenIdConnectUserTokenEndpoint)inner(provider)),
            registered.Lifetime));
    }
}
