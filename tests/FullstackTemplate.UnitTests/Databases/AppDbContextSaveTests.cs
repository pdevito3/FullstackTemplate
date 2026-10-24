namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Databases.Auditing;
using FullstackTemplate.Server.Domain.AuditEntries;
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
    public async Task save_refuses_an_audit_entry_that_code_added()
    {
        // Arrange
        await using var dbContext = CreateContext();
        var save = SaveFacts.From(DateTimeOffset.UtcNow, new AuditActor("sub-1", "spa", UserId: null, AgentId: null),
            requestName: null, traceId: null);
        var subject = new AuditSubject("User", "usr_1", "User", "usr_1", "ten_1");
        dbContext.AuditEntries.Add(new AuditEntry(save, 0, subject, AuditEntryKind.Deleted, action: null, "{}"));

        // Act
        var act = () => dbContext.SaveChangesAsync();

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldContain("Only the save writes audit entries");
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
            Substitute.For<ICallerIdentityResolver>(), Substitute.For<ITenantScopeProvider>(), new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()));
    }
}
