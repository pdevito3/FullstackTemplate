using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Management;

/// <summary>
/// The <c>returnUrl</c> query of <c>/bff/login</c> and <c>/bff/logout</c>: where the browser goes at the end.
/// </summary>
public static class ReturnUrl
{
    /// <summary>
    /// The longest return URL, after the percent-encoding of <see cref="LocalUrl.Value"/>. The return URL travels in
    /// the <c>state</c> of the authorize and end-session URLs, which grows by about 1.33 characters for each
    /// character of the return URL. Identity providers and proxies refuse a request line of more than about 8 KB
    /// with 414. At this limit, the authorize URL of ZeroFactor is about 3.7 KB. The end-session URL also holds the
    /// ID token in <c>id_token_hint</c>.
    /// </summary>
    public const int MaxLength = 2048;

    private const string QueryKey = "returnUrl";

    private static readonly ILogger Logger = Log.ForContext(typeof(ReturnUrl));

    /// <summary>
    /// The return URL of the request, or null when the query holds a URL that is not local. With no value, or with a
    /// value longer than <see cref="MaxLength"/>, it is <c>PathBase</c>, or <c>/</c>. A login or a logout that the
    /// identity provider refuses is worse than a return to the home page.
    /// </summary>
    public static LocalUrl? Read(HttpRequest request)
    {
        var value = request.Query[QueryKey].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
            return Default(request);

        if (!LocalUrl.TryParse(value, out var returnUrl))
            return null;

        if (returnUrl.Value.Length <= MaxLength)
            return returnUrl;

        Logger.Here().Information("The returnUrl has {Length} characters, more than {MaxLength}, so the browser goes to the default return URL",
            returnUrl.Value.Length, MaxLength);
        return Default(request);
    }

    /// <summary>
    /// The 400 problem for a request whose <c>returnUrl</c> is not local.
    /// </summary>
    public static Task WriteInvalidAsync(HttpContext context) =>
        ManagementProblem.WriteAsync(context, "Invalid return url", QueryKey, $"ReturnUrl '{context.Request.Query[QueryKey].FirstOrDefault()}' was invalid");

    private static LocalUrl Default(HttpRequest request) =>
        LocalUrl.TryParse(request.PathBase.ToUriComponent(), out var pathBase) ? pathBase : LocalUrl.Root;
}
