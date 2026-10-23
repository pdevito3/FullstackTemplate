namespace FullstackTemplate.Server.Services;

/// <summary>
/// The client IDs of the first-party apps: <c>Auth:ClientId</c> (the BFF) and <c>Swagger:ClientId</c> when it is set.
/// A token from one of these clients acts as its user, with all of the user's permissions.
/// </summary>
public sealed class FirstPartyClientOptions
{
    public required IReadOnlySet<string> ClientIds { get; init; }

    public bool Contains(string clientId) => ClientIds.Contains(clientId);

    /// <exception cref="InvalidOperationException"><c>Auth:ClientId</c> is missing.</exception>
    public static FirstPartyClientOptions FromConfiguration(IConfiguration configuration)
    {
        // Without it, the server cannot tell the app from an agent, and each user token is an unknown client.
        var authClientId = configuration["Auth:ClientId"]
            ?? throw new InvalidOperationException("Auth:ClientId configuration is required.");

        var clientIds = new HashSet<string>(StringComparer.Ordinal) { authClientId };
        // An empty value would make a token with an empty client claim first-party.
        if (configuration["Swagger:ClientId"] is { } swaggerClientId && !string.IsNullOrWhiteSpace(swaggerClientId))
            clientIds.Add(swaggerClientId);

        return new FirstPartyClientOptions { ClientIds = clientIds };
    }
}
