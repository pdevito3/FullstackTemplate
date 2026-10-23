namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

public sealed class AppDbContextSaveTests
{
    [Test]
    public void sync_save_is_refused()
    {
        using var dbContext = CreateContext();

        var exception = Should.Throw<NotSupportedException>(() => dbContext.SaveChanges());

        exception.Message.ShouldContain("SaveChangesAsync");
    }

    // The tests in this class do not connect to a database.
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=unused")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options, TimeProvider.System, Substitute.For<ICurrentUserService>(),
            Substitute.For<ITenantScopeProvider>(), Substitute.For<IMediator>());
    }
}
