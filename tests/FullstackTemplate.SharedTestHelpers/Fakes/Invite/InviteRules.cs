namespace FullstackTemplate.SharedTestHelpers.Fakes.Invite;

using Bogus;
using FullstackTemplate.Server.Domain.Users;

/// <summary>
/// The one definition of a realistic invite. Each method is named for the property that it fills.
/// The shape fakers and the entity graph blueprint use these methods, so a change here reaches all of them.
/// </summary>
public static class InviteRules
{
    public static string Role(Faker f) => f.PickRandom(UserRole.ListNames());

    public static string Email(Faker f) => f.Internet.Email();

    // The default of the spec: the link is the secret, so an invite does not require an email match
    public static bool RequireEmailMatch(Faker f) => false;
}
