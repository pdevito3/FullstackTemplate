using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace FullstackTemplate.AppHost.AuthProviders;

/// <summary>
/// Brings Pocket ID to the seed state through its admin API: the groups, the test users with fixed IDs, and the client
/// with a fixed ID, URLs, and secret. Each step reads the current state and creates or updates the item, so a start
/// with a kept data volume converges to the same state. The API reference is at /api/docs of Pocket ID.
/// </summary>
internal sealed partial class PocketIdSeeder(EndpointReference pocketId, ParameterResource staticApiKey, AuthClientUrls clientUrls)
{
    public const string ClientId = "aspire-app";
    public const string ClientSecret = "super-secret-client-secret-change-in-production";
    // The IDs of the Keycloak and ZeroFactor seeds, so the sub claim is the same with these providers
    public const string AdminUserId = "154c705b-f83d-4657-a9dd-af6ad1d36796";
    private const string UserId = "09d798af-976f-416a-8a28-001db736ab59";

    // Long enough for a working day. A login link works one time.
    private const string LoginLinkLifetime = "24h";

    private static readonly SeedUser[] Users =
    [
        new(AdminUserId, "admin@example.com", "Admin", "User", IsAdmin: true, ["admin", "user"]),
        new(UserId, "user@example.com", "Test", "User", IsAdmin: false, ["user"]),
    ];

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    // Set after the first seed of this AppHost run. The login links of the seed are in the log only one time.
    private bool _seeded;

    /// <summary>
    /// Seeds one time for each AppHost run, and again if the client is gone, for example after a restart of an
    /// isolated Pocket ID with no volume.
    /// </summary>
    public async Task<HealthCheckResult> CheckAsync(ILogger log, CancellationToken cancellationToken)
    {
        try
        {
            if (_seeded && await StatusAsync($"/api/oidc/clients/{ClientId}", cancellationToken) is HttpStatusCode.OK)
                return HealthCheckResult.Healthy();

            await SeedAsync(cancellationToken);
            _seeded = true;
            LogSeedComplete(log);
            await PrintLoginLinksAsync(log, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (HttpRequestException exception)
        {
            return HealthCheckResult.Unhealthy("Pocket ID does not answer yet.", exception);
        }
        catch (PocketIdSeedException exception)
        {
            LogSeedFailed(log, exception.Message);
            return HealthCheckResult.Unhealthy(exception.Message);
        }
    }

    public async Task PrintLoginLinksAsync(ILogger log, CancellationToken cancellationToken)
    {
        foreach (var user in Users)
        {
            using var response = await SendAsync(HttpMethod.Post, $"/api/users/{user.Id}/one-time-access-token",
                Json(new PocketIdLoginCodeRequest(LoginLinkLifetime), PocketIdJsonContext.Default.PocketIdLoginCodeRequest), cancellationToken);
            await EnsureSuccessAsync(response, "create a login code", cancellationToken);
            var code = await response.Content.ReadFromJsonAsync(PocketIdJsonContext.Default.PocketIdLoginCodeResponse, cancellationToken)
                ?? throw new PocketIdSeedException("Pocket ID answered the login code request with no body.");
            // The users are the test users of the seed, and a link works only on this local Pocket ID
            var link = $"{pocketId.Url}/lc/{code.Token}";
            // After the link, Pocket ID stays on its own pages. It cannot send the browser to the app.
            LogLoginLink(log, user.Email, link, clientUrls.Frontend.Url, LoginLinkLifetime);
        }
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        var groupIds = new Dictionary<string, string>();
        foreach (var group in Users.SelectMany(user => user.Groups).Distinct())
            groupIds[group] = await UpsertGroupAsync(group, cancellationToken);

        foreach (var user in Users)
        {
            var body = new PocketIdUserRequest(user.Id, user.Email, user.Email, EmailVerified: true, user.FirstName,
                user.LastName, $"{user.FirstName} {user.LastName}", user.IsAdmin, Disabled: false);
            await UpsertAsync($"/api/users/{user.Id}", "/api/users",
                () => Json(body, PocketIdJsonContext.Default.PocketIdUserRequest), $"the user {user.Email}", cancellationToken);
            var groups = new PocketIdUserGroupsRequest([.. user.Groups.Select(group => groupIds[group])]);
            await PutAsync($"/api/users/{user.Id}/user-groups",
                Json(groups, PocketIdJsonContext.Default.PocketIdUserGroupsRequest), $"the groups of {user.Email}", cancellationToken);
        }

        var client = await ClientRequestAsync(cancellationToken);
        await UpsertAsync($"/api/oidc/clients/{ClientId}", "/api/oidc/clients",
            () => Json(client, PocketIdJsonContext.Default.PocketIdClientRequest), $"the client {ClientId}", cancellationToken);
        await EnsureClientSecretAsync(cancellationToken);
    }

    private async Task<PocketIdClientRequest> ClientRequestAsync(CancellationToken cancellationToken)
    {
        // The container network view of the BFF, as WithClientUrls gives it to the other providers
        var backChannel = await ReferenceExpression.Create($"{clientUrls.BffForContainers}/bff/backchannel")
            .GetValueAsync(cancellationToken)
            ?? throw new PocketIdSeedException("The back-channel URL of the BFF has no value.");
        return new PocketIdClientRequest(
            ClientId,
            "Aspire App",
            CallbackURLs:
            [
                $"{clientUrls.Frontend.Url}/signin-oidc",
                $"{clientUrls.Bff.Url}/signin-oidc",
                $"{clientUrls.Server.Url}/swagger/oauth2-redirect.html",
                $"{clientUrls.ServerHttp.Url}/swagger/oauth2-redirect.html",
            ],
            LogoutCallbackURLs:
            [
                clientUrls.Frontend.Url,
                $"{clientUrls.Frontend.Url}/signout-callback-oidc",
                $"{clientUrls.Bff.Url}/signout-callback-oidc",
            ],
            BackchannelLogoutURL: backChannel,
            IsPublic: false,
            PkceEnabled: true,
            // A first-party app, so the user sees no consent page
            SkipConsent: true);
    }

    // Pocket ID makes a random secret for a new client. The seed adds the fixed secret only when a token request with
    // it fails, so that each start does not add one more secret.
    private async Task EnsureClientSecretAsync(CancellationToken cancellationToken)
    {
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, $"{pocketId.Url}/api/oidc/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" }),
        };
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}")));
        using var token = await Http.SendAsync(tokenRequest, cancellationToken);
        if (token.IsSuccessStatusCode)
            return;
        if (token.StatusCode is not HttpStatusCode.Unauthorized)
            await EnsureSuccessAsync(token, "check the client secret", cancellationToken);

        using var created = await SendAsync(HttpMethod.Post, $"/api/oidc/clients/{ClientId}/secrets",
            Json(new PocketIdClientSecretRequest(ClientSecret), PocketIdJsonContext.Default.PocketIdClientSecretRequest), cancellationToken);
        await EnsureSuccessAsync(created, "add the client secret", cancellationToken);
    }

    private async Task<string> UpsertGroupAsync(string name, CancellationToken cancellationToken)
    {
        using var list = await SendAsync(HttpMethod.Get, $"/api/user-groups?search={Uri.EscapeDataString(name)}", content: null, cancellationToken);
        await EnsureSuccessAsync(list, "list the groups", cancellationToken);
        var groups = await list.Content.ReadFromJsonAsync(PocketIdJsonContext.Default.PocketIdGroupList, cancellationToken)
            ?? throw new PocketIdSeedException("Pocket ID answered the group list request with no body.");
        var body = new PocketIdGroupRequest(name, name);
        if (groups.Data.SingleOrDefault(group => group.Name == name) is { } existing)
        {
            await PutAsync($"/api/user-groups/{existing.Id}", Json(body, PocketIdJsonContext.Default.PocketIdGroupRequest),
                $"the group {name}", cancellationToken);
            return existing.Id;
        }

        using var created = await SendAsync(HttpMethod.Post, "/api/user-groups",
            Json(body, PocketIdJsonContext.Default.PocketIdGroupRequest), cancellationToken);
        await EnsureSuccessAsync(created, $"create the group {name}", cancellationToken);
        var group = await created.Content.ReadFromJsonAsync(PocketIdJsonContext.Default.PocketIdGroup, cancellationToken)
            ?? throw new PocketIdSeedException($"Pocket ID answered the create of the group {name} with no body.");
        return group.Id;
    }

    // An item with a fixed ID: update it if it exists, else create it with the ID in the body
    private async Task UpsertAsync(string itemPath, string createPath, Func<HttpContent> body, string item, CancellationToken cancellationToken)
    {
        var status = await StatusAsync(itemPath, cancellationToken);
        if (status is HttpStatusCode.OK)
        {
            await PutAsync(itemPath, body(), item, cancellationToken);
            return;
        }
        if (status is not HttpStatusCode.NotFound)
            throw new PocketIdSeedException($"Pocket ID answered {(int)status} to the read of {item}.");

        using var created = await SendAsync(HttpMethod.Post, createPath, body(), cancellationToken);
        await EnsureSuccessAsync(created, $"create {item}", cancellationToken);
    }

    private async Task PutAsync(string path, HttpContent body, string item, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Put, path, body, cancellationToken);
        await EnsureSuccessAsync(response, $"update {item}", cancellationToken);
    }

    private async Task<HttpStatusCode> StatusAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, path, content: null, cancellationToken);
        return response.StatusCode;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, $"{pocketId.Url}{path}") { Content = content };
        request.Headers.Add("X-API-KEY", await staticApiKey.GetValueAsync(cancellationToken));
        return await Http.SendAsync(request, cancellationToken);
    }

    private static JsonContent Json<T>(T body, JsonTypeInfo<T> typeInfo) => JsonContent.Create(body, typeInfo);

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string action, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new PocketIdSeedException($"Pocket ID answered {(int)response.StatusCode} when the seed tried to {action}: {detail}");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The Pocket ID seed is complete.")]
    private static partial void LogSeedComplete(ILogger log);

    [LoggerMessage(Level = LogLevel.Error, Message = "The Pocket ID seed failed: {Reason}")]
    private static partial void LogSeedFailed(ILogger log, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sign in as {Email} with {LoginLink}, and then open {AppUrl}. The link works one time, for {Lifetime}.")]
    private static partial void LogLoginLink(ILogger log, string email, string loginLink, string appUrl, string lifetime);

    private sealed record SeedUser(string Id, string Email, string FirstName, string LastName, bool IsAdmin, string[] Groups);
}

internal sealed class PocketIdSeedException(string message) : Exception(message);

internal sealed class PocketIdSeedHealthCheck(PocketIdSeeder seeder, ILogger log) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        seeder.CheckAsync(log, cancellationToken);
}

// The bodies of the Pocket ID admin API that the seed sends and reads
internal sealed record PocketIdUserRequest(
    string Id, string Username, string Email, bool EmailVerified, string FirstName, string LastName, string DisplayName,
    bool IsAdmin, bool Disabled);

internal sealed record PocketIdUserGroupsRequest(string[] UserGroupIds);

internal sealed record PocketIdGroupRequest(string Name, string FriendlyName);

internal sealed record PocketIdGroup(string Id, string Name);

internal sealed record PocketIdGroupList(PocketIdGroup[] Data);

internal sealed record PocketIdClientRequest(
    string Id, string Name, string[] CallbackURLs, string[] LogoutCallbackURLs, string BackchannelLogoutURL,
    bool IsPublic, bool PkceEnabled, bool SkipConsent);

internal sealed record PocketIdClientSecretRequest(string Secret);

internal sealed record PocketIdLoginCodeRequest(string Ttl);

internal sealed record PocketIdLoginCodeResponse(string Token);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(PocketIdUserRequest))]
[JsonSerializable(typeof(PocketIdUserGroupsRequest))]
[JsonSerializable(typeof(PocketIdGroupRequest))]
[JsonSerializable(typeof(PocketIdGroup))]
[JsonSerializable(typeof(PocketIdGroupList))]
[JsonSerializable(typeof(PocketIdClientRequest))]
[JsonSerializable(typeof(PocketIdClientSecretRequest))]
[JsonSerializable(typeof(PocketIdLoginCodeRequest))]
[JsonSerializable(typeof(PocketIdLoginCodeResponse))]
internal sealed partial class PocketIdJsonContext : JsonSerializerContext;
