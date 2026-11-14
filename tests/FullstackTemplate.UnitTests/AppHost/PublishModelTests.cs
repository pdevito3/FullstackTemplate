namespace FullstackTemplate.UnitTests.AppHost;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using FullstackTemplate.AppHost;
using FullstackTemplate.AppHost.AuthProviders;
using Microsoft.Extensions.Configuration;
using Shouldly;

[NotInParallel(TestConstraints.HostEnvironment)]
public class PublishModelTests
{
    [Test]
    public async Task a_run_has_floci_storage()
    {
        // Act
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync(
            typeof(Isolation), [], (options, _) => options.DisableDashboard = true);

        // Assert
        builder.Resources.ShouldContain(resource => resource.Name == "storage");
    }

    private const string OperatorVariable = "BackgroundJobs__OperatorSubjects__0";

    public static IEnumerable<string> ProviderNames => AuthProviderCatalog.All.Select(provider => provider.Name);

    [Test]
    [MethodDataSource(nameof(ProviderNames))]
    public async Task a_run_makes_the_operator_subject_of_the_provider_an_operator_of_the_jobs_dashboard(string name)
    {
        // Arrange
        var expected = OperatorSubjectOf(name);

        // Act
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync(
            typeof(Isolation), [$"--AuthProvider={name}"], (options, _) => options.DisableDashboard = true);
        var operatorSubject = await ServerVariableAsync(builder, OperatorVariable);

        // Assert
        operatorSubject.ShouldBe(expected);
    }

    [Test]
    public async Task a_publish_makes_no_user_an_operator_of_the_jobs_dashboard()
    {
        // Act
        // ZeroFactor, the default provider, refuses a publish
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync(
            typeof(Isolation), ["--operation", "publish", "--publisher", "default", "--AuthProvider=Keycloak"], (options, _) => options.DisableDashboard = true);

        // Assert
        (await ServerVariableAsync(builder, OperatorVariable)).ShouldBeNull();
    }

    [Test]
    public async Task a_publish_has_no_floci_storage_with_test_credentials()
    {
        // Act
        // ZeroFactor, the default provider, refuses a publish
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync(
            typeof(Isolation), ["--operation", "publish", "--publisher", "default", "--AuthProvider=Keycloak"], (options, _) => options.DisableDashboard = true);

        // Assert
        builder.ExecutionContext.IsPublishMode.ShouldBeTrue();
        builder.Resources.ShouldNotContain(resource => resource.Name == "storage");
    }

    private static string? OperatorSubjectOf(string name)
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        var app = builder.AddContainer("app", "app").WithHttpEndpoint(name: "http");
        var clientUrls = new AuthClientUrls(app.GetEndpoint("http"), app.GetEndpoint("http"), app.GetEndpoint("http"), app.GetEndpoint("http"), app.GetEndpoint("http"));
        var settings = new ConfigurationBuilder()
            .AddInMemoryCollection([new(AuthProviderCatalog.SettingName, name)])
            .Build();
        return AuthProviderCatalog.Select(settings).AddTo(builder, clientUrls).OperatorSubject;
    }

    // The callbacks only add values. The test reads one plain value and resolves no endpoint.
    private static async Task<string?> ServerVariableAsync(IDistributedApplicationTestingBuilder builder, string variable)
    {
        var server = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "server");
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, server);
        foreach (var annotation in server.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await annotation.Callback(context);

        return context.EnvironmentVariables.TryGetValue(variable, out var value) ? (string)value : null;
    }
}
