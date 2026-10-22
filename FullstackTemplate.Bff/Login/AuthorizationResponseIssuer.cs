using FullstackTemplate.Bff.Features;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Primitives;

namespace FullstackTemplate.Bff.Login;

/// <summary>
/// The <c>iss</c> check of the authorization response (RFC 9207 section 2.4), a defense against mix-up attacks.
/// It runs in <c>OnMessageReceived</c>, before the correlation check and before the handler reads <c>error</c> or
/// <c>code</c>. Thus it also covers an error response.
/// </summary>
public static class AuthorizationResponseIssuer
{
    public const string Parameter = "iss";

    /// <summary>
    /// The <c>OnMessageReceived</c> step. A failure goes to <c>OnRemoteFailure</c> as a
    /// <see cref="CallbackRejectedException"/>.
    /// </summary>
    public static void Check(MessageReceivedContext context)
    {
        var features = context.HttpContext.RequestServices.GetRequiredService<ProviderFeatures>().Active;
        // The method guard allows only a GET, so the response is in the query
        var failure = Validate(context.Request.Query[Parameter], features.IssuerParameter, features.Issuer);
        if (failure is not null)
            context.Fail(new CallbackRejectedException(Parameter, failure));
    }

    /// <summary>
    /// Null when the <c>iss</c> values pass the rule, or the reason that they fail. The compare is ordinal.
    /// </summary>
    public static string? Validate(StringValues iss, IssuerParameterRule rule, string issuer)
    {
        if (rule == IssuerParameterRule.Disabled)
            return null;

        if (iss.Count > 1)
            return "The authorization response has more than one iss parameter.";

        var value = iss.FirstOrDefault();
        if (value is null)
            return rule == IssuerParameterRule.Required
                ? "The authorization response has no iss parameter, and the identity provider sends one."
                : null;

        return string.Equals(value, issuer, StringComparison.Ordinal)
            ? null
            : "The iss parameter of the authorization response is not the issuer of the identity provider.";
    }
}
