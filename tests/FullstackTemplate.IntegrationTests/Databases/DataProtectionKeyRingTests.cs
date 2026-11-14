namespace FullstackTemplate.IntegrationTests.Databases;

using System.Security.Cryptography;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Databases.DataProtection;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

public sealed class DataProtectionKeyRingTests : IAsyncDisposable
{
    private const string Purpose = "key-ring-test";

    private readonly PostgresTestDatabase _otherDatabase = new();

    public async ValueTask DisposeAsync() => await _otherDatabase.DisposeAsync();

    // A machine with no key ring in the database keeps one in a folder of the user. The process on the other database
    // proves that the processes share the database, not that folder.
    [Test]
    public async Task a_process_on_the_same_database_unprotects_a_payload_and_a_process_on_another_database_does_not()
    {
        // Arrange
        await _otherDatabase.StartAsync();
        await using var first = TestFixture.BuildServiceProvider(_ => { });
        await using var sameDatabase = TestFixture.BuildServiceProvider(_ => { });
        await using var otherDatabase = TestFixture.BuildServiceProvider(_ => { }, _otherDatabase.ConnectionString);
        await new MigrationHostedService(
                otherDatabase.GetRequiredService<IServiceScopeFactory>(), otherDatabase.GetRequiredService<IConfiguration>())
            .StartingAsync(CancellationToken.None);
        var payload = $"antiforgery-{Guid.NewGuid()}";

        // Act
        var protectedPayload = ProtectorOf(first).Protect(payload);

        // Assert
        ProtectorOf(sameDatabase).Unprotect(protectedPayload).ShouldBe(payload);
        Should.Throw<CryptographicException>(() => ProtectorOf(otherDatabase).Unprotect(protectedPayload));
    }

    [Test]
    public async Task the_key_ring_is_in_the_data_protection_schema_of_appdb()
    {
        // Arrange
        await using var services = TestFixture.BuildServiceProvider(_ => { });

        // Act
        ProtectorOf(services).Protect("payload");

        // Assert
        await using var connection = new NpgsqlConnection(TestFixture.UnpooledConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"SELECT count(*) FROM {DataProtectionKeysDbContext.Schema}.data_protection_keys", connection);
        ((long)(await command.ExecuteScalarAsync())!).ShouldBeGreaterThan(0);
    }

    private static IDataProtector ProtectorOf(IServiceProvider services)
        => services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);
}
