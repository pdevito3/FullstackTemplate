namespace FullstackTemplate.IntegrationTests.FeatureTests.Invites;

internal static class InviteScopes
{
    /// <summary>A signed-in user of the first-party client with no user record, as a colleague who opens the link.</summary>
    public static string SignInAsNewUser(this TestingServiceScope scope, string? email = null, bool emailVerified = true)
    {
        var subject = $"sub-{Guid.NewGuid():N}";
        scope.SetUserWithDefaults(identifier: subject, email: email, emailVerified: emailVerified);
        return subject;
    }
}
