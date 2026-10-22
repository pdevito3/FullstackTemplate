namespace FullstackTemplate.Bff.ApiEndpoints;

/// <summary>
/// Endpoint metadata for a BFF API endpoint: an endpoint that the SPA calls with <c>fetch</c>, not with a
/// navigation. A BFF API endpoint requires the <c>X-CSRF: 1</c> header (<see cref="CsrfHeaderMiddleware"/>),
/// and its challenge and forbid give a 401 and a 403 instead of a 302 (<see cref="BffApiAuthenticationService"/>).
/// </summary>
public sealed class BffApiEndpointMetadata
{
    public static readonly BffApiEndpointMetadata Instance = new();

    private BffApiEndpointMetadata()
    {
    }
}

public static class BffApiEndpointExtensions
{
    /// <summary>
    /// Marks the endpoint as a BFF API endpoint. See <see cref="BffApiEndpointMetadata"/>.
    /// </summary>
    public static TBuilder AsBffApiEndpoint<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(BffApiEndpointMetadata.Instance);

    /// <summary>
    /// True when the endpoint of the request is a BFF API endpoint. False before routing selects an endpoint.
    /// </summary>
    public static bool IsBffApiEndpoint(this HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<BffApiEndpointMetadata>() is not null;
}
