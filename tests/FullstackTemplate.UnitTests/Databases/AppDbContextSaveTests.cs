namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
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

    [Test]
    public void contexts_with_the_handler_write_guard_share_one_internal_service_provider()
    {
        using var first = CreateContext();
        using var second = CreateContext();

        second.GetService<IModelSource>().ShouldBeSameAs(first.GetService<IModelSource>());
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=unused")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options, TimeProvider.System, Substitute.For<ICurrentUserService>(),
            Substitute.For<ITenantScopeProvider>(), new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()));
    }
}
