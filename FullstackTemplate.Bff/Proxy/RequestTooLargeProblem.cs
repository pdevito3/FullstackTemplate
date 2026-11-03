using Yarp.ReverseProxy.Forwarder;

namespace FullstackTemplate.Bff.Proxy;

/// <summary>
/// Gives a problem body to the 413 of the proxy. When the body of a request is larger than the limit of Kestrel,
/// YARP stops the forward and sends a 413 with no body, so the SPA has no <c>detail</c> to show. In practice only a
/// file upload gets that large.
/// </summary>
public static class RequestTooLargeProblem
{
    public const string Detail = "The file is too large to send. Choose a smaller file.";

    public static IApplicationBuilder UseRequestTooLargeProblem(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            await next(context);

            // Only the forwarder sets the error feature, so a 413 of the API itself keeps its own body
            if (context.Response.HasStarted
                || context.Response.StatusCode != StatusCodes.Status413PayloadTooLarge
                || context.Features.Get<IForwarderErrorFeature>() is null)
                return;

            await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = { Status = StatusCodes.Status413PayloadTooLarge, Detail = Detail },
            });
        });
}
