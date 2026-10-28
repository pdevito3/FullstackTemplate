namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using Microsoft.EntityFrameworkCore;
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

    private sealed class NonUniqueIndexContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.MapConstraintErrors();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Gadget>().HasIndex(g => g.Code).ForRequestField("code", "A gadget with this code already exists.");
    }

    private sealed class Gadget
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
    }
}
