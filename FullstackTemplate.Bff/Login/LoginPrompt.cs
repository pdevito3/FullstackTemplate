using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace FullstackTemplate.Bff.Login;

/// <summary>
/// The <c>prompt</c> parameter of <c>/bff/login</c>. The value travels in the authentication properties, which
/// the OIDC handler protects in the <c>state</c> parameter, from the login request to the authorize request and
/// back to the callback.
/// </summary>
public static class LoginPrompt
{
    private const string PromptKey = "bff-prompt";

    public static void Store(AuthenticationProperties properties, string prompt) =>
        properties.Items[PromptKey] = prompt;

    /// <summary>
    /// The <c>OnRedirectToIdentityProvider</c> step: copies the stored prompt to the authorize request.
    /// </summary>
    public static void CopyToAuthorizeRequest(RedirectContext context)
    {
        if (TryGet(context.Properties, out var prompt))
            context.ProtocolMessage.Prompt = prompt;
    }

    /// <summary>
    /// The <c>OnMessageReceived</c> event. When a login with a prompt gets an <c>error</c> (for example
    /// <c>login_required</c> for <c>prompt=none</c>), the callback sends the browser to the return URL instead of
    /// failing the request. The return URL comes from the protected state, so it passed the local URL rule.
    /// </summary>
    public static Task RedirectOnPromptError(MessageReceivedContext context)
    {
        if (context.Properties is { RedirectUri: { } redirectUri } properties
            && TryGet(properties, out _)
            && context.ProtocolMessage.Error is not null)
        {
            context.HandleResponse();
            context.Response.Redirect(redirectUri);
        }

        return Task.CompletedTask;
    }

    private static bool TryGet(AuthenticationProperties properties, out string prompt)
    {
        prompt = properties.Items.TryGetValue(PromptKey, out var value) ? value ?? "" : "";
        return !string.IsNullOrEmpty(prompt);
    }
}
