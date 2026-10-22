using System.Net.Http.Headers;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.DPoP;
using FullstackTemplate.Bff.Proxy;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.DPoP;

/// <summary>
/// DPoP on the userinfo call of a login (RFC 9449 section 7.1). The OIDC handler sends the access token as
/// <c>Bearer</c>. A DPoP-bound token must go as <c>Authorization: DPoP</c>, with a proof that has the hash of the
/// token (<c>ath</c>).
/// <para>
/// Token management puts the DPoP key of the code exchange in <c>HttpContext.Items</c> and never removes it. Its
/// backchannel handler then adds a proof without <c>ath</c> to each later request of the callback, the userinfo
/// call too. A strict provider rejects that proof. Thus <see cref="BindAfterCodeExchange"/> removes the key after
/// the code exchange, and <see cref="Handler"/> sends the userinfo call with the DPoP scheme and a correct proof.
/// </para>
/// </summary>
public static class UserInfoDPoP
{
    // The name of token management for the key of the code exchange
    private const string CodeExchangeKeyItem = "dpop_proof_key";
    private const string DPoPHeaderName = "DPoP";
    private static readonly object BindingItem = new();
    private static readonly ILogger Logger = Log.ForContext(typeof(UserInfoDPoP));

    private sealed record Binding(string AccessToken, DPoPProofKey Key);

    /// <summary>
    /// The <c>OnTokenValidated</c> event, after the code exchange and before the userinfo call. Removes the key of
    /// the code exchange, and keeps the key and the token for <see cref="Handler"/> when the token is DPoP-bound.
    /// </summary>
    public static void BindAfterCodeExchange(TokenValidatedContext context)
    {
        var items = context.HttpContext.Items;
        if (!items.TryGetValue(CodeExchangeKeyItem, out var value))
            return;

        items.Remove(CodeExchangeKeyItem);
        if (value is not DPoPProofKey key)
            return;

        if (context.TokenEndpointResponse is { AccessToken: { Length: > 0 } accessToken, TokenType: var tokenType }
            && string.Equals(tokenType, "DPoP", StringComparison.OrdinalIgnoreCase))
        {
            items[BindingItem] = new Binding(accessToken, key);
        }
    }

    /// <summary>
    /// The backchannel handler of the OIDC handler. For the userinfo call with a DPoP-bound token, it sets
    /// <c>Authorization: DPoP</c> and a proof with <c>ath</c>. After <c>use_dpop_nonce</c> it sends the call one
    /// more time with the nonce. Other requests go through unchanged.
    /// </summary>
    public sealed class Handler(IHttpContextAccessor httpContextAccessor, IDPoPProofService proofService) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (httpContextAccessor.HttpContext?.Items[BindingItem] is not Binding binding
                || request.Headers.Authorization is not { Scheme: var scheme, Parameter: var token }
                || !string.Equals(scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
                || token != binding.AccessToken)
            {
                return await base.SendAsync(request, cancellationToken);
            }

            await SetHeadersAsync(request, binding, nonce: null, cancellationToken);
            var response = await base.SendAsync(request, cancellationToken);
            if (!DPoPProxy.UsesNonceError(response, out var nonce))
                return response;

            response.Dispose();
            await SetHeadersAsync(request, binding, nonce, cancellationToken);
            Logger.Here().Debug("Sent the userinfo request again with the DPoP nonce that the identity provider gave");
            return await base.SendAsync(request, cancellationToken);
        }

        private async Task SetHeadersAsync(HttpRequestMessage request, Binding binding, DPoPNonce? nonce, CancellationToken cancellationToken)
        {
            var proof = await proofService.CreateProofTokenAsync(new DPoPProofRequest
            {
                Url = request.GetDPoPUrl(),
                Method = request.Method,
                DPoPProofKey = binding.Key,
                AccessToken = AccessToken.Parse(binding.AccessToken),
                DPoPNonce = nonce,
            }, cancellationToken) ?? throw new InvalidOperationException("Could not create the DPoP proof for the userinfo request.");

            request.Headers.Authorization = new AuthenticationHeaderValue("DPoP", binding.AccessToken);
            request.Headers.Remove(DPoPHeaderName);
            request.Headers.TryAddWithoutValidation(DPoPHeaderName, proof.ToString());
        }
    }
}
