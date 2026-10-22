using System.Net;
using System.Net.Http.Headers;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OpenIdConnect;
using Serilog;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Proxy;

/// <summary>
/// What the nonce retry needs to build a second proof for a proxied request.
/// </summary>
internal sealed record DPoPProxyRequest(string AccessToken, DPoPProofKey Key, Uri Url);

/// <summary>
/// DPoP on the API calls (RFC 9449 section 7): a DPoP-bound token goes as <c>Authorization: DPoP</c>, with a new
/// proof in the <c>DPoP</c> header for each request. The proof has the method, the URL of the API without query
/// and fragment, the hash of the token (<c>ath</c>), and the nonce of the API, if it gave one.
/// </summary>
public static class DPoPProxy
{
    /// <summary>
    /// A request with a body larger than this gets no retry after <c>use_dpop_nonce</c>, because the BFF would
    /// have to hold the body in memory.
    /// </summary>
    public const int MaxRetryBodyBytes = 64 * 1024;

    private const string DPoPHeaderName = "DPoP";
    private const string NonceHeaderName = "DPoP-Nonce";
    private static readonly ILogger Logger = Log.ForContext(typeof(DPoPProxy));
    private static readonly HttpRequestOptionsKey<DPoPProxyRequest> RequestKey = new("bff.dpop.request");

    /// <summary>
    /// Sets <c>Authorization: DPoP</c> and the proof on the proxy request. Returns false when the session has no
    /// key for the token, which is a token from before DPoP was on.
    /// </summary>
    internal static async ValueTask<bool> AttachAsync(RequestTransformContext context, UserToken token)
    {
        if (token.DPoPJsonWebKey is not { } key)
        {
            Logger.Here().Error("Forwarded {Path} with no access token, because the token is a DPoP token and the BFF session has no DPoP key",
                context.HttpContext.Request.Path.Value);
            return false;
        }

        // The URL that the proxy sends the request to, without the query (RFC 9449 section 4.2: htu has no query)
        var url = RequestUtilities.MakeDestinationAddress(context.DestinationPrefix, context.Path, QueryString.Empty);
        var proxyRequest = new DPoPProxyRequest(token.AccessToken.ToString(), key, url);
        await SetHeadersAsync(context.HttpContext.RequestServices, context.ProxyRequest, proxyRequest, nonce: null, context.HttpContext.RequestAborted);
        context.ProxyRequest.Options.Set(RequestKey, proxyRequest);
        return true;
    }

    private static async Task SetHeadersAsync(IServiceProvider services, HttpRequestMessage request, DPoPProxyRequest proxyRequest,
        DPoPNonce? nonce, CancellationToken cancellationToken)
    {
        var proof = await services.GetRequiredService<IDPoPProofService>().CreateProofTokenAsync(new DPoPProofRequest
        {
            Url = proxyRequest.Url,
            Method = request.Method,
            DPoPProofKey = proxyRequest.Key,
            AccessToken = AccessToken.Parse(proxyRequest.AccessToken),
            DPoPNonce = nonce,
        }, cancellationToken) ?? throw new InvalidOperationException("Could not create the DPoP proof for the API call.");

        request.Headers.Authorization = new AuthenticationHeaderValue("DPoP", proxyRequest.AccessToken);
        request.Headers.Remove(DPoPHeaderName);
        request.Headers.TryAddWithoutValidation(DPoPHeaderName, proof.ToString());
    }

    /// <summary>
    /// True for a <c>401</c> with <c>WWW-Authenticate: DPoP error="use_dpop_nonce"</c> and a <c>DPoP-Nonce</c>
    /// header (RFC 9449 section 9). <paramref name="nonce"/> is then the nonce.
    /// </summary>
    internal static bool UsesNonceError(HttpResponseMessage response, out DPoPNonce nonce)
    {
        nonce = default;
        if (response.StatusCode != HttpStatusCode.Unauthorized
            || !response.Headers.TryGetValues(NonceHeaderName, out var values)
            || DPoPNonce.ParseOrDefault(values.FirstOrDefault()) is not { } parsed)
        {
            return false;
        }

        nonce = parsed;
        return response.Headers.WwwAuthenticate.Any(c =>
            string.Equals(c.Scheme, "DPoP", StringComparison.OrdinalIgnoreCase)
            && c.Parameter?.Contains("error=\"use_dpop_nonce\"", StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// Wraps the forwarder handler with the nonce retry.
    /// </summary>
    internal sealed class NonceRetryClientFactory(IServiceProvider services) : ForwarderHttpClientFactory
    {
        protected override HttpMessageHandler WrapHandler(ForwarderHttpClientContext context, HttpMessageHandler handler) =>
            new NonceRetryHandler(services, base.WrapHandler(context, handler));
    }

    /// <summary>
    /// An API that wants a nonce answers <c>401</c> with <c>WWW-Authenticate: DPoP error="use_dpop_nonce"</c> and
    /// a <c>DPoP-Nonce</c> header (RFC 9449 section 9). The handler stores the nonce, so that the next proof
    /// for the URL has it, and sends the request one more time with a new proof. It retries once.
    /// </summary>
    private sealed class NonceRetryHandler(IServiceProvider services, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!request.Options.TryGetValue(RequestKey, out var proxyRequest))
                return await base.SendAsync(request, cancellationToken);

            // A body streams once. Hold a small body, so that the retry can send it again.
            byte[]? body = null;
            var canRetry = true;
            if (request.Content is { } content)
            {
                if (content.Headers.ContentLength is { } length and <= MaxRetryBodyBytes)
                    body = await content.ReadAsByteArrayAsync(cancellationToken);
                else
                    canRetry = false;
            }

            if (body is not null)
                request.Content = Replayable(request.Content!, body);

            var response = await base.SendAsync(request, cancellationToken);
            if (!UsesNonceError(response, out var nonce))
                return response;

            await using (var scope = services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<IDPoPNonceStore>().StoreNonceAsync(
                    new DPoPNonceContext { Url = proxyRequest.Url, Method = request.Method }, nonce, cancellationToken);
                if (!canRetry)
                {
                    Logger.Here().Information("The API asked for a DPoP nonce, but the request body is too large to send again. The next request has the nonce");
                    return response;
                }

                response.Dispose();
                if (body is not null)
                    request.Content = Replayable(request.Content!, body);
                await SetHeadersAsync(scope.ServiceProvider, request, proxyRequest, nonce, cancellationToken);
            }

            Logger.Here().Debug("Sent {Method} to the API again with the DPoP nonce that it gave", request.Method.Method);
            return await base.SendAsync(request, cancellationToken);
        }

        private static ByteArrayContent Replayable(HttpContent original, byte[] body)
        {
            var copy = new ByteArrayContent(body);
            foreach (var header in original.Headers)
                copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
            return copy;
        }
    }
}
