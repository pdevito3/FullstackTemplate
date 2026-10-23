namespace FullstackTemplate.IntegrationTests.FeatureTests.Agents;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;

internal static class AgentTestCallers
{
    /// <summary>
    /// Adds a user with the role User to the seed tenant, gives the user only <paramref name="permissions"/>, and signs
    /// the scope in as that user through the first-party client.
    /// </summary>
    public static async Task<User> SignInAsMemberWithOnlyAsync(
        this TestingServiceScope scope, params Permission[] permissions)
    {
        var graph = scope.StartGraph(out _).AddUser(out var member, u => u.Role = "User");
        member.AddPermissions(permissions);
        await graph.BuildAsync();

        scope.SetUserWithDefaults(identifier: member.Identifier);
        return member;
    }
}
