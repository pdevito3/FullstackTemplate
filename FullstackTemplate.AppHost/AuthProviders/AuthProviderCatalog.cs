using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

namespace FullstackTemplate.AppHost.AuthProviders;

/// <summary>
/// An identity provider that the AppHost can add to the stack. The class <c>&lt;Name&gt;AuthProvider</c> lives in the
/// folder AuthProviders/&lt;Name&gt;/, with its seed files in seed/. The e2e tests read its login form from
/// frontend/e2e/idps/&lt;name&gt;.json. To delete a provider, delete its folder and its e2e file.
/// </summary>
public interface IAuthProvider
{
    /// <summary>
    /// The value of the AuthProvider setting that selects this provider. It comes from the class name, so that the
    /// class, the folder, and the setting cannot differ.
    /// </summary>
    string Name => GetType().Name[..^AuthProviderCatalog.ClassSuffix.Length];

    /// <summary>
    /// Adds the resources of the provider to the stack and returns the settings for the server and the BFF.
    /// </summary>
    AuthProviderConfig AddTo(IDistributedApplicationBuilder builder, AuthClientUrls clientUrls);
}

/// <summary>
/// Finds the providers in this assembly and selects one with the AuthProvider setting.
/// </summary>
public static class AuthProviderCatalog
{
    public const string SettingName = "AuthProvider";
    public const string ClassSuffix = "AuthProvider";

    public static IReadOnlyList<IAuthProvider> All { get; } = typeof(IAuthProvider).Assembly.GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false } && type.IsAssignableTo(typeof(IAuthProvider)))
        .Select(type => type.Name.EndsWith(ClassSuffix, StringComparison.Ordinal) && type.Name != ClassSuffix
            ? type
            : throw new InvalidOperationException($"The provider class {type.Name} must have the name <Name>{ClassSuffix}."))
        .Select(type => (IAuthProvider)Activator.CreateInstance(type)!)
        .OrderBy(provider => provider.Name, StringComparer.Ordinal)
        .ToList();

    public static IAuthProvider Select(IConfiguration configuration)
    {
        var known = string.Join(", ", All.Select(provider => provider.Name));
        var name = configuration[SettingName];
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException($"Set {SettingName} to one of these providers: {known}.");

        return All.SingleOrDefault(provider => string.Equals(provider.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"{SettingName} '{name}' is not known. Use one of these providers: {known}.");
    }
}

public static class AuthProviderSeedExtensions
{
    /// <summary>
    /// Mounts the seed/ folder of the provider into the container, read-only.
    /// </summary>
    public static IResourceBuilder<ContainerResource> WithSeedFiles(
        this IResourceBuilder<ContainerResource> container, IAuthProvider provider, string target) =>
        container.WithBindMount($"./AuthProviders/{provider.Name}/seed", target, isReadOnly: true);
}
