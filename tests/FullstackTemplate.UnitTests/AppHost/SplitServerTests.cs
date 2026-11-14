namespace FullstackTemplate.UnitTests.AppHost;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using FullstackTemplate.AppHost;
using Shouldly;

[NotInParallel(TestConstraints.HostEnvironment)]
public class SplitServerTests
{
    private const string Split = $"--{SplitServer.SettingName}=true";
    private const string RoleVariable = "Server__Role";

    [Test]
    public async Task a_run_has_one_server_with_the_default_role()
    {
        // Act
        await using var builder = await CreateAsync();
        var server = await EnvironmentOfAsync(builder, "server");

        // Assert
        builder.Resources.ShouldNotContain(resource => resource.Name == SplitServer.WorkerName);
        server.ShouldNotContainKey(RoleVariable);
    }

    [Test]
    public async Task a_split_run_gives_the_server_the_role_api_and_the_worker_the_role_worker()
    {
        // Act
        await using var builder = await CreateAsync(Split);
        var server = await EnvironmentOfAsync(builder, "server");
        var worker = await EnvironmentOfAsync(builder, SplitServer.WorkerName);

        // Assert
        server[RoleVariable].ShouldBe("Api");
        worker[RoleVariable].ShouldBe("Worker");
    }

    [Test]
    public async Task the_worker_gets_the_database_the_storage_and_the_client_of_the_app_and_no_other_auth_setting()
    {
        // Act
        await using var builder = await CreateAsync(Split);
        var worker = await EnvironmentOfAsync(builder, SplitServer.WorkerName);
        var waits = Resource(builder, SplitServer.WorkerName).Annotations.OfType<WaitAnnotation>().Select(wait => wait.Resource.Name);

        // Assert
        worker.ShouldContainKey("ConnectionStrings__appdb");
        worker["ObjectStorage__BucketName"].ShouldBe("files");
        worker.Keys.Where(key => key.StartsWith("Auth__", StringComparison.Ordinal)).ShouldBe(["Auth__ClientId"]);
        worker.ShouldNotContainKey("Mcp__PublicOrigin");
        worker.ShouldNotContainKey("BackgroundJobs__OperatorSubjects__0");
        waits.ShouldBe(["postgres", "appdb", "storage"], ignoreOrder: true, "The worker does not wait for the auth provider.");
    }

    [Test]
    public async Task the_bff_calls_only_the_server_in_a_split_run()
    {
        // Act
        await using var builder = await CreateAsync(Split);
        var references = Resource(builder, "bff").Annotations.OfType<ResourceRelationshipAnnotation>()
            .Where(relationship => relationship.Type == "Reference")
            .Select(relationship => relationship.Resource.Name);

        // Assert
        references.ShouldContain("server");
        references.ShouldNotContain(SplitServer.WorkerName);
    }

    [Test]
    public void a_publish_with_a_split_server_stops_the_apphost()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--operation", "publish", Split],
            DisableDashboard = true,
        });

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => SplitServer.IsOn(builder));

        // Assert
        exception.Message.ShouldContain("Server__Role");
    }

    private static Task<IDistributedApplicationTestingBuilder> CreateAsync(params string[] args) =>
        DistributedApplicationTestingBuilder.CreateAsync(typeof(Isolation), args, (options, _) => options.DisableDashboard = true);

    private static ProjectResource Resource(IDistributedApplicationTestingBuilder builder, string name) =>
        builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == name);

    // The callbacks only add values. The test resolves no endpoint and no connection string.
    private static async Task<Dictionary<string, object>> EnvironmentOfAsync(IDistributedApplicationTestingBuilder builder, string name)
    {
        var resource = Resource(builder, name);
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, resource);
        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await annotation.Callback(context);

        return context.EnvironmentVariables;
    }
}
