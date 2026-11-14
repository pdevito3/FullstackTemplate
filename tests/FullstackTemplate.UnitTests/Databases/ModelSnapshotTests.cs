namespace FullstackTemplate.UnitTests.Databases;

using System.Reflection;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Databases.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

public class ModelSnapshotTests
{
    // `dotnet ef migrations has-pending-model-changes` compares only the DDL, so it misses a change with no DDL, such as a navigation.
    [Test]
    public void the_committed_model_snapshot_is_the_snapshot_that_the_next_migration_writes()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act & Assert
        SnapshotShouldBeCurrent(context, Path.Combine("Databases", "Migrations", "AppDbContextModelSnapshot.cs"));
    }

    [Test]
    public void the_committed_model_snapshot_of_the_data_protection_keys_is_the_snapshot_that_the_next_migration_writes()
    {
        // Arrange
        using var context = new DataProtectionKeysDbContextFactory().CreateDbContext([]);

        // Act & Assert
        SnapshotShouldBeCurrent(context, Path.Combine("Databases", "DataProtection", "Migrations", "DataProtectionKeysDbContextModelSnapshot.cs"));
    }

    private static void SnapshotShouldBeCurrent(DbContext context, string snapshotPathInServer)
    {
        var services = new ServiceCollection()
            .AddEntityFrameworkDesignTimeServices()
            .AddDbContextDesignTimeServices(context);
        ProviderDesignTimeServices().ConfigureDesignTimeServices(services);
        using var provider = services.BuildServiceProvider();
        var scaffolder = provider.GetRequiredService<IMigrationsScaffolder>();
        var committedPath = Path.Combine(FindRepositoryRoot(), "FullstackTemplate.Server", snapshotPathInServer);

        var scaffolded = ModelLines(scaffolder.ScaffoldMigration("Probe", "FullstackTemplate.Server").SnapshotCode);
        var committed = ModelLines(File.ReadAllText(committedPath));

        var line = committed.Zip(scaffolded).TakeWhile(pair => pair.First == pair.Second).Count();
        committed.SequenceEqual(scaffolded).ShouldBeTrue(
            $"The committed model snapshot of {context.GetType().Name} is not current. Line {line + 1} is `{LineAt(committed, line)}`, " +
            $"but the next migration writes `{LineAt(scaffolded, line)}`. " +
            "Add a migration to update the snapshot. See docs/backend.md#migrations.");
    }

    // The EF Core version is not part of the model. The order of the usings changes when scripts/init.cs renames the app.
    private static string[] ModelLines(string snapshot)
        => snapshot.ReplaceLineEndings("\n")
            .Split('\n')
            .Select(line => line.StartsWith("using ", StringComparison.Ordinal)
                || line.Contains(""".HasAnnotation("ProductVersion",""", StringComparison.Ordinal) ? string.Empty : line)
            .ToArray();

    private static string LineAt(string[] lines, int index)
        => index < lines.Length ? lines[index] : "(end of file)";

    // The same lookup as `dotnet ef`: the provider names its design-time services in an assembly attribute.
    private static IDesignTimeServices ProviderDesignTimeServices()
    {
        var provider = typeof(NpgsqlDbContextOptionsBuilderExtensions).Assembly;
        var typeName = provider.GetCustomAttribute<DesignTimeProviderServicesAttribute>()!.TypeName;
        return (IDesignTimeServices)Activator.CreateInstance(provider.GetType(typeName, throwOnError: true)!)!;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FullstackTemplate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
