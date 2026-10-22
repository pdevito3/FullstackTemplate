using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FullstackTemplate.Bff.Login;

/// <summary>
/// The <c>OnTokenValidated</c> event, after the handler validated the ID token of the token response.
/// </summary>
public static class IdTokenChecks
{
    public const string AudienceClaim = "aud";
    public const string AlgorithmHeader = "alg";

    public static Task ValidateAsync(TokenValidatedContext context)
    {
        // The handler turns off RequireSignedTokens for an ID token from the token endpoint (Core 3.1.3.7 lets TLS
        // stand in for the signature). The client is registered for one algorithm, so alg=none is never valid.
        // The at_hash check below needs a hash algorithm from alg, so this check runs first.
        var algorithmFailure = CheckAlgorithm(context.SecurityToken.Header.Alg, context.Options.TokenValidationParameters.ValidAlgorithms);
        if (algorithmFailure is not null)
        {
            context.Fail(new CallbackRejectedException(AlgorithmHeader, algorithmFailure));
            return Task.CompletedTask;
        }

        // The code redemption of TokenClientAuthentication makes the handler skip these checks: the ID token and
        // access token are present, the ID token claims (Core 3.1.3.7), the nonce, and at_hash.
        if (context.TokenEndpointResponse is not null)
        {
            context.Options.ProtocolValidator.ValidateTokenResponse(new OpenIdConnectProtocolValidationContext
            {
                ClientId = context.Options.ClientId,
                ProtocolMessage = context.TokenEndpointResponse,
                ValidatedIdToken = context.SecurityToken,
                Nonce = context.Nonce,
            });
        }

        var failure = CheckAudience(context.SecurityToken.Audiences, context.Options.ClientId!);
        if (failure is not null)
            context.Fail(new CallbackRejectedException(AudienceClaim, failure));

        return Task.CompletedTask;
    }

    /// <summary>
    /// The ID token must have one of the valid algorithms of the client registration. Null when it passes.
    /// </summary>
    public static string? CheckAlgorithm(string? algorithm, IEnumerable<string>? validAlgorithms) =>
        validAlgorithms?.Contains(algorithm, StringComparer.Ordinal) == true
            ? null
            : $"The ID token has the algorithm '{algorithm}', which the client registration does not allow.";

    /// <summary>
    /// The strict audience check: the <c>aud</c> of the ID token holds only the client id (Core 3.1.3.7, no
    /// untrusted audience). Null when it passes. A custom <c>IdTokenValidator</c> would turn off the default checks,
    /// so this check is an event.
    /// </summary>
    public static string? CheckAudience(IEnumerable<string> audiences, string clientId)
    {
        var list = audiences.ToList();
        return list.Count == 1 && string.Equals(list[0], clientId, StringComparison.Ordinal)
            ? null
            : $"The aud of the ID token must hold only the client id, but it has {list.Count} audiences.";
    }
}
