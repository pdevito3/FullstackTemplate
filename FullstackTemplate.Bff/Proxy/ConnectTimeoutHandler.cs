namespace FullstackTemplate.Bff.Proxy;

/// <summary>
/// Makes a connect timeout a connection error, so that YARP answers 502 and not 504. A connect timeout comes as a
/// <see cref="TaskCanceledException"/>, which YARP reports as a timeout of the request. But the request never
/// reached the server, as when the Aspire proxy of a stopped server holds the TLS handshake. The 504 of the BFF
/// means that the server can still complete the request, and the SPA tells the user to reload to see it.
/// </summary>
internal sealed class ConnectTimeoutHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        // SocketsHttpHandler gives a TimeoutException as the inner exception only for its ConnectTimeout
        catch (OperationCanceledException ex) when (ex.InnerException is TimeoutException && !cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException(HttpRequestError.ConnectionError, ex.InnerException.Message, ex);
        }
    }
}
