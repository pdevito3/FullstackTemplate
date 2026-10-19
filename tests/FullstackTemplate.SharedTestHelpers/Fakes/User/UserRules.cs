namespace FullstackTemplate.SharedTestHelpers.Fakes.User;

using Bogus;
using FullstackTemplate.Server.Domain.Users;

/// <summary>
/// The one definition of a realistic user. Each method is named for the property that it fills.
/// The shape fakers and the entity graph blueprint use these methods, so a change here reaches all of them.
/// </summary>
public static class UserRules
{
    public static string FirstName(Faker f) => f.Name.FirstName();
    public static string LastName(Faker f) => f.Name.LastName();
    public static string Identifier(Faker f) => f.Random.Guid().ToString();
    public static string Email(Faker f) => f.Internet.Email();
    public static string Username(Faker f) => f.Internet.UserName();
    public static string Role(Faker f) => f.PickRandom(UserRole.ListNames());
}
