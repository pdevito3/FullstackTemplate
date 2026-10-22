using FullstackTemplate.Bff.Management;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace FullstackTemplate.Bff.Login;

/// <summary>
/// <c>/bff/login</c> starts a login: a challenge on the OIDC scheme, which sends the browser to the identity
/// provider. The SPA navigates here, so the response stays a 302.
/// </summary>
public static class LoginEndpoint
{
    public const string Path = "/bff/login";

    public static async Task HandleAsync(HttpContext context, IOptionsMonitor<OpenIdConnectOptions> oidcOptions)
    {
        var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault();
        var prompt = context.Request.Query["prompt"].FirstOrDefault();

        var configuration = await OidcDiscovery.GetConfigurationAsync(oidcOptions, context.RequestAborted);
        if (configuration is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (prompt is not null && !configuration.PromptValuesSupported.Contains(prompt))
        {
            await ManagementProblem.WriteAsync(context, "Invalid prompt value", "prompt", $"prompt '{prompt}' is not supported");
            return;
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && !LocalUrl.IsLocal(returnUrl))
        {
            await ManagementProblem.WriteAsync(context, "Invalid return url", "returnUrl", $"ReturnUrl '{returnUrl}' was invalid");
            return;
        }

        if (string.IsNullOrWhiteSpace(returnUrl))
            returnUrl = context.Request.PathBase.HasValue ? context.Request.PathBase.Value! : "/";

        var properties = new AuthenticationProperties { RedirectUri = returnUrl };
        if (prompt is not null)
            LoginPrompt.Store(properties, prompt);

        await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, properties);
    }
}
