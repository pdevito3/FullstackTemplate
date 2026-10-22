namespace FullstackTemplate.UnitTests.AppHost;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using FullstackTemplate.AppHost.AuthProviders;
using Microsoft.Extensions.Configuration;
using Shouldly;

// An app can delete the providers that it does not use, so these tests name no provider. They cover the providers that exist.
public class AuthProviderCatalogTests
{
    public static IEnumerable<string> ProviderNames => AuthProviderCatalog.All.Select(provider => provider.Name);

    [Test]
    public void the_catalog_finds_at_least_one_provider_and_each_name_is_unique()
    {
        // Act
        var names = ProviderNames.ToList();

        // Assert
        names.ShouldNotBeEmpty();
        names.ShouldBeUnique(StringComparer.OrdinalIgnoreCase);
    }

    [Test]
    [MethodDataSource(nameof(ProviderNames))]
    public void the_setting_selects_the_provider_with_that_name_in_any_case(string name)
    {
        // Act
        var provider = AuthProviderCatalog.Select(Settings(name.ToUpperInvariant()));

        // Assert
        provider.Name.ShouldBe(name);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("NotAProvider")]
    public void a_missing_or_unknown_setting_stops_the_apphost_with_the_known_providers(string? name)
    {
        // Act
        var exception = Should.Throw<InvalidOperationException>(() => AuthProviderCatalog.Select(Settings(name)));

        // Assert
        foreach (var known in ProviderNames)
            exception.Message.ShouldContain(known);
    }

    [Test]
    public void the_default_in_appsettings_names_a_provider_that_exists()
    {
        // Arrange
        var appSettings = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppHostSourceDirectory(), "appsettings.json"))
            .Build();

        // Act
        var provider = AuthProviderCatalog.Select(appSettings);

        // Assert
        provider.Name.ShouldBe(appSettings[AuthProviderCatalog.SettingName]);
    }

    [Test]
    [MethodDataSource(nameof(ProviderNames))]
    public void each_provider_has_its_folder_and_its_e2e_login_form(string name)
    {
        // Arrange
        var appHost = AppHostSourceDirectory();
        var root = Path.GetDirectoryName(appHost)!;
        var provider = AuthProviderCatalog.Select(Settings(name));

        // Assert
        File.Exists(Path.Combine(appHost, "AuthProviders", name, $"{name}AuthProvider.cs")).ShouldBeTrue(
            $"The class {name}AuthProvider must be in AuthProviders/{name}/, because scripts/init.cs finds the providers by folder.");
        File.Exists(Path.Combine(root, "frontend", "e2e", "idps", $"{name.ToLowerInvariant()}.json")).ShouldBe(provider.RunsE2eTests,
            $"The e2e tests need the login form of {name} in frontend/e2e/idps/{name.ToLowerInvariant()}.json, but only if RunsE2eTests is true.");
    }

    [Test]
    [MethodDataSource(nameof(ProviderNames))]
    public void each_provider_gives_an_authority_and_mounts_seed_files_that_exist(string name)
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        var app = builder.AddContainer("app", "app").WithHttpEndpoint(name: "http");
        var clientUrls = new AuthClientUrls(app.GetEndpoint("http"), app.GetEndpoint("http"), app.GetEndpoint("http"), app.GetEndpoint("http"));

        // Act
        var config = AuthProviderCatalog.Select(Settings(name)).AddTo(builder, clientUrls);

        // Assert
        (config.Authority is null).ShouldNotBe(config.AuthResource is null, "A provider sets either a static authority or a container.");
        var seedMounts = builder.Resources
            .SelectMany(resource => resource.Annotations.OfType<ContainerMountAnnotation>())
            .Where(mount => mount.Type == ContainerMountType.BindMount)
            .Select(mount => Path.GetRelativePath(builder.AppHostDirectory, mount.Source!));
        foreach (var seed in seedMounts)
            Directory.Exists(Path.Combine(AppHostSourceDirectory(), seed)).ShouldBeTrue($"The seed folder {seed} does not exist.");
    }

    private static IConfiguration Settings(string? authProvider) => new ConfigurationBuilder()
        .AddInMemoryCollection([new(AuthProviderCatalog.SettingName, authProvider)])
        .Build();

    private static string AppHostSourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory.GetFiles("*.slnx").Length == 0)
            directory = directory.Parent ?? throw new InvalidOperationException("The test found no .slnx file above its folder.");

        return directory.GetDirectories("*.AppHost").Single().FullName;
    }
}
