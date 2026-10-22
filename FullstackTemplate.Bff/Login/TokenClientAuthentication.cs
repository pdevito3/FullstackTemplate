using System.Net.Http.Headers;
using System.Text;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.IdentityModel.Client;
using FullstackTemplate.Bff.Authentication;
using FullstackTemplate.Bff.Features;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FullstackTemplate.Bff.Login;

/// <summary>
/// The client authentication at the token endpoint, <c>client_secret_basic</c> or <c>client_secret_post</c>
/// (<see cref="ActiveFeatures.ClientAuthentication"/>), for each request with client authentication: the code
/// exchange, the refresh, the revocation, and the PAR request.
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

        BindCodeExchangeToDPoPKey(context);
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

        DefaultAccessTokenLifetime.ApplyTo(tokenResponse, context.HttpContext.RequestServices.GetRequiredService<IConfiguration>());
        context.HandleCodeRedemption(tokenResponse);
    }

    /// <summary>
    /// The <c>OnPushAuthorization</c> event: the PAR request uses the client authentication of the token endpoint
    /// (RFC 9126 section 2). The handler always sends <c>client_secret_post</c>, and the event gives no HTTP
    /// request. Thus for <c>client_secret_basic</c> this event pushes the request itself, through
    /// <c>context.Options.Backchannel</c>, and gives the <c>request_uri</c> to the handler with <c>HandlePush</c>.
    /// </summary>
    public static async Task PushAuthorizationAsync(PushedAuthorizationContext context)
    {
        var features = context.HttpContext.RequestServices.GetRequiredService<ProviderFeatures>().Active;
        if (features.ClientAuthentication != ClientAuthenticationMethod.Basic)
            return;

        var cancellationToken = context.HttpContext.RequestAborted;
        var configuration = context.Options.ConfigurationManager is { } manager
            ? await manager.GetConfigurationAsync(cancellationToken)
            : context.Options.Configuration ?? throw new InvalidOperationException("The OIDC handler has no discovery document.");

        // The client_id stays in the body, as in the example of RFC 9126 section 2.1
        var parameters = new Dictionary<string, string>(context.ProtocolMessage.Parameters);
        parameters.Remove(OpenIdConnectParameterNames.ClientSecret);
        using var request = new HttpRequestMessage(HttpMethod.Post, configuration.PushedAuthorizationRequestEndpoint);
        request.Headers.Authorization = BasicHeader(context.Options.ClientId!, context.Options.ClientSecret!);
        request.Content = new FormUrlEncodedContent(parameters);
        request.Version = context.Options.Backchannel.DefaultRequestVersion;

        using var response = await context.Options.Backchannel.SendAsync(request, cancellationToken);
        OpenIdConnectMessage parResponse;
        try
        {
            parResponse = new OpenIdConnectMessage(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            throw new OpenIdConnectProtocolException(
                $"Failed to parse the PAR response body as JSON. Status Code: {(int)response.StatusCode}. Content-Type: {response.Content.Headers.ContentType}", ex);
        }

        if (!response.IsSuccessStatusCode || string.IsNullOrEmpty(parResponse.RequestUri))
        {
            throw new OpenIdConnectProtocolException(
                $"The PAR endpoint returned {(int)response.StatusCode} with error '{parResponse.Error}', error_description '{parResponse.ErrorDescription}', and no request_uri.");
        }

        // HandlePush alone: the handler allows only one of HandlePush, SkipPush, and HandleClientAuthentication
        context.HandlePush(parResponse.RequestUri);
    }

    /// <summary>
    /// Gives the DPoP key of the login to the DPoP handler of token management, which then adds the proof to the
    /// token request. The authorize redirect stored the key in the authentication properties. The handler reads it
    /// from <c>HttpContext.Items</c>, and token management copies it there only after this event returns, which is
    /// too late for a request that this event sends itself. The two names below are those of token management.
    /// </summary>
    private static void BindCodeExchangeToDPoPKey(AuthorizationCodeReceivedContext context)
    {
        if (context.Properties?.Items.TryGetValue(".Token.dpop_proof_key", out var key) == true && !string.IsNullOrEmpty(key))
            context.HttpContext.Items["dpop_proof_key"] = DPoPProofKey.Parse(key);
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
