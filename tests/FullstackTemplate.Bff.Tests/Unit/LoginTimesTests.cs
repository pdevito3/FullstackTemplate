namespace FullstackTemplate.Bff.Tests.Unit;

using System.Security.Claims;
using FullstackTemplate.Bff.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

public class LoginTimesTests
{
    private static readonly DateTimeOffset Deadline = new(2026, 1, 1, 12, 15, 0, TimeSpan.Zero);

    [Test]
    public void the_tokens_of_a_login_are_issued_at_the_iat_of_its_id_token()
    {
        // Act
        var login = LoginTimes.Of(Ticket(IdToken("""{"iat":1767268900}""")));

        // Assert
        login.TokensIssuedAt.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1767268900));
        login.Deadline.ShouldBe(Deadline);
    }

    [Test]
    public void a_logout_issued_up_to_5_s_before_the_tokens_ends_the_login()
    {
        // Arrange
        var issued = Deadline.AddMinutes(-10);

        // Act
        var login = new LoginTimes(issued, Deadline);

        // Assert
        login.EndedByLogoutsFrom.ShouldBe(issued.AddSeconds(-5));
    }

    [Test]
    public void a_ticket_with_no_deadline_is_refused()
    {
        // Arrange
        var ticket = Ticket(IdToken("""{"iat":1767268900}"""));
        ticket.Properties.Items.Clear();

        // Act & Assert
        Should.Throw<LoginRefusedException>(() => LoginTimes.Of(ticket));
    }

    [Test]
    public void a_ticket_with_no_id_token_is_a_fault()
    {
        // Arrange
        var ticket = Ticket(idToken: null);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => LoginTimes.Of(ticket));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void an_id_token_with_no_readable_iat_is_a_fault(bool encrypted)
    {
        // Arrange
        var idToken = encrypted
            ? new JsonWebTokenHandler().CreateToken("""{"iat":1767268900}""", new EncryptingCredentials(
                new SymmetricSecurityKey(new byte[32]), SecurityAlgorithms.Aes256KW, SecurityAlgorithms.Aes128CbcHmacSha256))
            : IdToken("""{"sub":"user-1"}""");

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => LoginTimes.Of(Ticket(idToken)));
    }

    [Test]
    public void a_login_is_refused_from_its_deadline_on()
    {
        // Arrange
        var login = new LoginTimes(Deadline.AddMinutes(-15), Deadline);

        // Act & Assert
        Should.NotThrow(() => login.ThrowIfPastDeadline(Deadline.AddTicks(-1)));
        Should.Throw<LoginRefusedException>(() => login.ThrowIfPastDeadline(Deadline));
    }

    private static string IdToken(string payload) => new JsonWebTokenHandler().CreateToken(payload);

    private static AuthenticationTicket Ticket(string? idToken)
    {
        var properties = new AuthenticationProperties();
        LoginTimes.SetDeadline(properties, Deadline);
        if (idToken is not null)
            properties.StoreTokens([new AuthenticationToken { Name = "id_token", Value = idToken }]);
        return new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([], "test")), properties, "Cookies");
    }
}
