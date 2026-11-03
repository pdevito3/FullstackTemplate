namespace FullstackTemplate.UnitTests.AppHost;

using Aspire.Hosting.Testing;
using FullstackTemplate.AppHost;
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
}
