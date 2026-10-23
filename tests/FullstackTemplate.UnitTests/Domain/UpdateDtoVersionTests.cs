namespace FullstackTemplate.UnitTests.Domain;

using System.Reflection;
using System.Runtime.CompilerServices;
using FullstackTemplate.Server.Domain;
using Shouldly;

public class UpdateDtoVersionTests
{
    [Test]
    public void every_update_dto_requires_the_version_that_the_caller_loaded()
    {
        // Arrange
        var updateDtos = typeof(BaseEntity).Assembly.GetTypes()
            .Where(t => t.Name.EndsWith("ForUpdateDto", StringComparison.Ordinal))
            .ToList();

        // Act
        var failures = updateDtos
            .Where(t => t.GetProperty(nameof(BaseEntity.Version)) is not { } version
                        || version.PropertyType != typeof(uint)
                        || version.GetCustomAttribute<RequiredMemberAttribute>() is null)
            .Select(t => t.Name)
            .ToList();

        // Assert
        updateDtos.ShouldNotBeEmpty();
        failures.ShouldBeEmpty();
    }
}
