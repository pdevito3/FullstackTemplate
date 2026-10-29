namespace FullstackTemplate.Bff.Tests.Unit;

using System.ComponentModel.DataAnnotations;
using FullstackTemplate.Bff.Authentication;
using Shouldly;

public class RefreshOptionsTests
{
    [Test]
    public void the_defaults_are_valid()
    {
        // Act & Assert
        Errors(new RefreshOptions()).ShouldBeEmpty();
    }

    [Test]
    [Arguments(5, 5)]
    [Arguments(6, 5)]
    public void a_token_request_timeout_that_is_not_less_than_the_lock_timeout_is_invalid(int tokenSeconds, int lockSeconds)
    {
        // Arrange
        var options = new RefreshOptions
        {
            TokenRequestTimeout = TimeSpan.FromSeconds(tokenSeconds),
            LockTimeout = TimeSpan.FromSeconds(lockSeconds),
        };

        // Act & Assert
        Errors(options).ShouldHaveSingleItem()
            .ShouldBe("Bff:Refresh:TokenRequestTimeout must be less than Bff:Refresh:LockTimeout.");
    }

    // The same check as ValidateDataAnnotations: the attributes, then IValidatableObject
    private static List<string?> Errors(RefreshOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results.Select(r => r.ErrorMessage).ToList();
    }
}
