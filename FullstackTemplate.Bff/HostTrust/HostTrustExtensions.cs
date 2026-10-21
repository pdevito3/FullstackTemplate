using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.HttpOverrides;

namespace FullstackTemplate.Bff.HostTrust;

/// <summary>
/// The forwarded headers, the host filter, and the public origin (section 3.8 of the spec).
/// </summary>
public static class HostTrustExtensions
{
    public const string AdditionalAllowedHostsKey = "Bff:AdditionalAllowedHosts";

    /// <summary>
    /// Reads <c>Bff:PublicOrigin</c> and <c>Bff:AdditionalAllowedHosts</c>, and configures the forwarded headers and
    /// the host filter.
    /// <list type="bullet">
    ///   <item>The forwarded headers give only the client address and the scheme. The BFF does not read
    ///   <c>X-Forwarded-Host</c>: the host comes from the public origin.</item>
    ///   <item><c>AllowedHosts</c> holds the host of the public origin and the hosts in
    ///   <c>Bff:AdditionalAllowedHosts</c>, separated by <c>;</c>. A request with another <c>Host</c> header gets a
    ///   400. The host filter of the web host defaults runs first in the pipeline, before
    ///   <c>UseForwardedHeaders</c> and <see cref="UsePublicOrigin"/>, so it checks the <c>Host</c> header that the
    ///   request has.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddHostTrust(this IServiceCollection services)
    {
        // From the final configuration, not the one at registration, so that a test host can set the values.
        // UsePublicOrigin resolves it while the app builds its pipeline, so a missing value stops the startup.
        services.AddSingleton(provider => PublicOrigin.FromConfiguration(provider.GetRequiredService<IConfiguration>()));

        services.AddOptions<HostFilteringOptions>()
            .Configure<PublicOrigin, IConfiguration>((options, publicOrigin, configuration) =>
                options.AllowedHosts = (configuration[AdditionalAllowedHostsKey] ?? "")
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Prepend(publicOrigin.Host.Host)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList());

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // The proxy in front of the BFF, such as AWS or Azure, ends the TLS. Its address is not
            // known, so trust the two headers from any proxy. The host and the redirect URIs never come from them.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }

    /// <summary>
    /// Sets <c>Request.Scheme</c> and <c>Request.Host</c> to the public origin, so that the OIDC handler builds
    /// <c>redirect_uri</c> and <c>post_logout_redirect_uri</c> from it. It runs after <c>UseForwardedHeaders</c>.
    /// </summary>
    public static IApplicationBuilder UsePublicOrigin(this IApplicationBuilder app)
    {
        var publicOrigin = app.ApplicationServices.GetRequiredService<PublicOrigin>();
        return app.Use((context, next) =>
        {
            context.Request.Scheme = publicOrigin.Scheme;
            context.Request.Host = publicOrigin.Host;
            return next(context);
        });
    }
}
