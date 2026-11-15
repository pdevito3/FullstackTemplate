using FullstackTemplate.Bff.Management;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Login;

/// <summary>
/// <c>/bff/login</c> starts a login: a challenge on the OIDC scheme, which sends the browser to the identity
/// provider. The SPA navigates here, so the response stays a 302.
/// </summary>
public static class LoginEndpoint
{
    private static readonly ILogger Logger = Log.ForContext(typeof(LoginEndpoint));

    public const string Path = "/bff/login";

    public static async Task HandleAsync(HttpContext context, IOptionsMonitor<OpenIdConnectOptions> oidcOptions, SpaOrigin spaOrigin)
    {
        var prompt = context.Request.Query["prompt"].FirstOrDefault();

        var configuration = await OidcDiscovery.GetConfigurationAsync(oidcOptions, context.RequestAborted);
        if (configuration is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (prompt is not null && !configuration.PromptValuesSupported.Contains(prompt))
        {
            Logger.Here().Warning("Refused a login with a prompt that the identity provider does not support: {Prompt}", LogValue.Clean(prompt));
            await ManagementProblem.RefuseAsync(context, AuthFailurePage.SignIn, "Invalid prompt value", "prompt", $"prompt '{prompt}' is not supported");
            return;
        }

        if (ReturnUrl.Read(context.Request) is not { } returnUrl)
        {
            await ReturnUrl.RefuseInvalidAsync(context, AuthFailurePage.SignIn);
            return;
        }

        var properties = new AuthenticationProperties { RedirectUri = spaOrigin.Resolve(returnUrl) };
        if (prompt is not null)
            LoginPrompt.Store(properties, prompt);

        await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, properties);
    }
}
