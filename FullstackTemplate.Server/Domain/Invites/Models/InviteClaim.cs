namespace FullstackTemplate.Server.Domain.Invites.Models;

/// <summary>The user that a claim adds, and the email in the token of the caller for the email match.</summary>
public sealed record InviteClaim
{
    public string Identifier { get; init; } = default!;
    public string? TokenEmail { get; init; }
    /// <summary>True only when the token says that the provider verified <see cref="TokenEmail"/>.</summary>
    public bool TokenEmailVerified { get; init; }
    public string FirstName { get; init; } = default!;
    public string LastName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Username { get; init; } = default!;
}
