using System.Net.Http.Headers;
using System.Text;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.IdentityModel.Client;
using FullstackTemplate.Bff.Features;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FullstackTemplate.Bff.Login;

/// <summary>
/// The client authentication at the token endpoint, <c>client_secret_basic</c> or <c>client_secret_post</c>
/// (<see cref="ActiveFeatures.ClientAuthentication"/>), for each token request: the code exchange, the refresh,
/// and the revocation.
/// <para>
/// The Microsoft handler always uses <c>client_secret_post</c> for the code exchange, and its
/// <c>OnAuthorizationCodeReceived</c> event gives only the token request message, not the HTTP request. Thus
/// <see cref="RedeemCodeAsync"/> redeems the code itself, through <c>context.Backchannel</c>, so that the DPoP
/// handler of token management still applies in phase 2.
/// </para>
/// </summary>
public sealed class TokenClientAuthentication(IOptions<UserTokenManagementOptions> tokenManagementOptions)
{
    /// <summary>
    /// Sets the method for the refresh and the revocation through token management. Its options are a singleton
    /// that the OIDC handler options already read, so the method goes on that instance after the decision.
    /// </summary>
    public void Apply(ClientAuthenticationMethod method) =>
        tokenManagementOptions.Value.ClientCredentialStyle = method == ClientAuthenticationMethod.Post
            ? ClientCredentialStyle.PostBody
            : ClientCredentialStyle.AuthorizationHeader;

    /// <summary>
    /// The <c>OnAuthorizationCodeReceived</c> event: sends the token request with the active client
    /// authentication and gives the response to the handler with <c>HandleCodeRedemption</c>. The handler then
    /// skips its own checks of the token response, so <see cref="IdTokenChecks"/> runs them.
    /// </summary>
    public static async Task RedeemCodeAsync(AuthorizationCodeReceivedContext context)
    {
        var cancellationToken = context.HttpContext.RequestAborted;
        var features = context.HttpContext.RequestServices.GetRequiredService<ProviderFeatures>().Active;
        var tokenRequest = context.TokenEndpointRequest
            ?? throw new InvalidOperationException("The OIDC handler gave no token request.");

        var tokenEndpoint = tokenRequest.TokenEndpoint;
        if (string.IsNullOrEmpty(tokenEndpoint) && context.Options.ConfigurationManager is { } manager)
            tokenEndpoint = (await manager.GetConfigurationAsync(cancellationToken)).TokenEndpoint;

        var parameters = new Dictionary<string, string>(tokenRequest.Parameters);
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
        if (features.ClientAuthentication == ClientAuthenticationMethod.Basic)
        {
            parameters.Remove(OpenIdConnectParameterNames.ClientId);
            parameters.Remove(OpenIdConnectParameterNames.ClientSecret);
            request.Headers.Authorization = BasicHeader(context.Options.ClientId!, context.Options.ClientSecret!);
        }

        request.Content = new FormUrlEncodedContent(parameters);
        request.Version = context.Backchannel.DefaultRequestVersion;

        using var response = await context.Backchannel.SendAsync(request, cancellationToken);
        OpenIdConnectMessage tokenResponse;
        try
        {
            tokenResponse = new OpenIdConnectMessage(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            throw new OpenIdConnectProtocolException(
                $"Failed to parse token response body as JSON. Status Code: {(int)response.StatusCode}. Content-Type: {response.Content.Headers.ContentType}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new OpenIdConnectProtocolException(
                $"The token endpoint returned {(int)response.StatusCode} with error '{tokenResponse.Error}', error_description '{tokenResponse.ErrorDescription}', and error_uri '{tokenResponse.ErrorUri}'.");
        }

        context.HandleCodeRedemption(tokenResponse);
    }

    /// <summary>
    /// <c>Authorization: Basic</c> with the client id and the secret. Each one is form-encoded before the Base64
    /// step (RFC 6749 section 2.3.1).
    /// </summary>
    public static AuthenticationHeaderValue BasicHeader(string clientId, string clientSecret)
    {
        var credentials = $"{FormEncode(clientId)}:{FormEncode(clientSecret)}";
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
    }

    // application/x-www-form-urlencoded (RFC 6749 appendix B): a space becomes +, and the other reserved
    // characters are percent-encoded
    private static string FormEncode(string value) => Uri.EscapeDataString(value).Replace("%20", "+", StringComparison.Ordinal);
}
