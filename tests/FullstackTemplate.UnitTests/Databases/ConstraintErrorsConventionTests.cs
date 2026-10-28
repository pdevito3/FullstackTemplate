namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shouldly;

public class ConstraintErrorsConventionTests
{
    [Test]
    public void request_field_on_an_index_that_is_not_unique_fails_the_model_build_and_names_the_index()
    {
        // Arrange
        using var dbContext = new NonUniqueIndexContext();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => dbContext.Model);

        // Assert
        exception.Message.ShouldContain("{'Code'}");
        exception.Message.ShouldContain(nameof(Gadget));
        exception.Message.ShouldContain("not unique");
    }

    [Test]
    public void stale_version_on_an_index_that_is_not_unique_fails_the_model_build_and_names_the_index()
    {
        // Arrange
        using var dbContext = new NonUniqueStaleVersionContext();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => dbContext.Model);

        // Assert
        exception.Message.ShouldContain("{'Code'}");
        exception.Message.ShouldContain(nameof(Gadget));
        exception.Message.ShouldContain("ForStaleVersion");
        exception.Message.ShouldContain("not unique");
    }

    [Test]
    public void index_with_a_request_field_and_a_stale_version_fails_the_model_build_and_names_the_index()
    {
        // Arrange
        using var dbContext = new BothDeclarationsContext();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => dbContext.Model);

        // Assert
        exception.Message.ShouldContain("{'Code'}");
        exception.Message.ShouldContain(nameof(Gadget));
        exception.Message.ShouldContain("both ForRequestField and ForStaleVersion");
    }

    // EF caches a model for each context type, so each index has a context type of its own
    private sealed class NonUniqueIndexContext : GadgetContext
    {
        protected override void Configure(IndexBuilder<Gadget> index)
            => index.ForRequestField("code", "A gadget with this code already exists.");
    }

    private sealed class NonUniqueStaleVersionContext : GadgetContext
    {
        protected override void Configure(IndexBuilder<Gadget> index) => index.ForStaleVersion();
    }

    private sealed class BothDeclarationsContext : GadgetContext
    {
        protected override void Configure(IndexBuilder<Gadget> index)
            => index.IsUnique().ForRequestField("code", "A gadget with this code already exists.").ForStaleVersion();
    }

    private abstract class GadgetContext : DbContext
    {
        protected abstract void Configure(IndexBuilder<Gadget> index);

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.MapConstraintErrors();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => Configure(modelBuilder.Entity<Gadget>().HasIndex(g => g.Code));
    }

    private sealed class Gadget
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
    }
}
