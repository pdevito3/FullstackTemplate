using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FullstackTemplate.Bff.Sessions;

/// <summary>
/// The times of a login that the store checks when it creates the row of the login
/// (<see cref="IBffSessionStore.CreateAsync"/>).
/// </summary>
/// <param name="TokensIssuedAt">The <c>iat</c> of the ID token: when the identity provider issued the tokens of the
/// login.</param>
/// <param name="Deadline">The challenge of the login plus <c>RemoteAuthenticationTimeout</c>. At this time the login
/// fails.</param>
public sealed record LoginTimes(DateTimeOffset TokensIssuedAt, DateTimeOffset Deadline)
{
    private const string DeadlineItem = "app.login.deadline";

    private static readonly TimeSpan InstanceClockSkew = TokenValidationParameters.DefaultClockSkew;

    private static readonly TimeSpan IdpClockSkew = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The earliest <c>iat</c> of a back-channel logout of the IdP session that ends the login. The identity
    /// provider issued an earlier logout before the tokens of the login, so the login is a new one, for example of an
    /// IdP session that the identity provider kept after the logout.
    /// </summary>
    public DateTimeOffset EndedByLogoutsFrom => TokensIssuedAt - IdpClockSkew;

    /// <summary>
    /// The <c>OnRedirectToIdentityProvider</c> step. It puts the deadline in the authentication properties, which
    /// travel in the data-protected <c>state</c>, so the identity provider and the browser cannot change it.
    /// </summary>
    public static void CaptureDeadline(RedirectContext context) =>
        SetDeadline(context.Properties, DeadlineOf(
            context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(),
            context.Options.RemoteAuthenticationTimeout));

    /// <summary>
    /// The latest deadline of a login whose challenge came before <paramref name="time"/> on any BFF instance.
    /// </summary>
    public static DateTimeOffset LatestDeadlineBefore(DateTimeOffset time, TimeSpan remoteAuthenticationTimeout) =>
        DeadlineOf(time, remoteAuthenticationTimeout) + InstanceClockSkew;

    public static void SetDeadline(AuthenticationProperties properties, DateTimeOffset deadline) =>
        properties.Items[DeadlineItem] = deadline.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>
    /// The times of the login of <paramref name="ticket"/>. A ticket with no deadline did not come from a challenge
    /// of this BFF, and its login fails.
    /// </summary>
    public static LoginTimes Of(AuthenticationTicket ticket)
    {
        if (!ticket.Properties.Items.TryGetValue(DeadlineItem, out var value)
            || !DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var deadline))
            throw new LoginRefusedException("the login has no deadline");

        var idToken = ticket.Properties.GetTokenValue("id_token");
        if (idToken is null || !new JsonWebTokenHandler().CanReadToken(idToken)
            || !new JsonWebToken(idToken).TryGetPayloadValue<long>("iat", out var issuedAt))
            throw new InvalidOperationException("A BFF session needs the signed id_token of its login, with an iat.");

        return new LoginTimes(DateTimeOffset.FromUnixTimeSeconds(issuedAt), deadline);
    }

    private static DateTimeOffset DeadlineOf(DateTimeOffset challenge, TimeSpan remoteAuthenticationTimeout) =>
        challenge + remoteAuthenticationTimeout;

    public void ThrowIfPastDeadline(DateTimeOffset now)
    {
        if (now >= Deadline)
            throw new LoginRefusedException("the login reached its deadline before it wrote its BFF session");
    }
}

/// <summary>
/// The store did not create the row of a login: a back-channel logout of its IdP session came first, or the login
/// reached its deadline. The callback answers 400 (<c>CallbackRejection.UseLoginRefusedProblem</c>), and the user
/// starts a new login.
/// </summary>
public sealed class LoginRefusedException(string reason) : Exception(reason);
