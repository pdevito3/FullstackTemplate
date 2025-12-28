namespace FullstackTemplate.UnitTests.Domain.EmailAddresses;

using FluentValidation;
using FullstackTemplate.Server.Domain.EmailAddresses;
using Shouldly;

public class EmailAddressTests
{
    [Fact]
    public void can_create_valid_email_address()
    {
        // Arrange & Act
        var email = EmailAddress.Of("test@example.com");

        // Assert
        email.Value.ShouldBe("test@example.com");
    }

    [Fact]
    public void can_create_email_with_various_valid_formats()
    {
        // Arrange & Act & Assert
        EmailAddress.Of("user@domain.com").Value.ShouldBe("user@domain.com");
        EmailAddress.Of("user.name@domain.com").Value.ShouldBe("user.name@domain.com");
        EmailAddress.Of("user+tag@domain.com").Value.ShouldBe("user+tag@domain.com");
        EmailAddress.Of("user@subdomain.domain.com").Value.ShouldBe("user@subdomain.domain.com");
    }

    [Fact]
    public void null_or_whitespace_email_returns_null_value()
    {
        // Arrange & Act
        var nullEmail = EmailAddress.Of(null!);
        var emptyEmail = EmailAddress.Of("");
        var whitespaceEmail = EmailAddress.Of("   ");

        // Assert
        nullEmail.Value.ShouldBeNull();
        emptyEmail.Value.ShouldBeNull();
        whitespaceEmail.Value.ShouldBeNull();
    }

    [Fact]
    public void invalid_email_throws_validation_exception()
    {
        // Arrange & Act & Assert
        Should.Throw<ValidationException>(() => EmailAddress.Of("invalid"));
        Should.Throw<ValidationException>(() => EmailAddress.Of("@nodomain.com"));
        Should.Throw<ValidationException>(() => EmailAddress.Of("noatsign.com"));
    }

    [Fact]
    public void email_can_be_implicitly_converted_to_string()
    {
        // Arrange
        var email = EmailAddress.Of("test@example.com");

        // Act
        string emailString = email;

        // Assert
        emailString.ShouldBe("test@example.com");
    }

    [Fact]
    public void email_equality_works_correctly()
    {
        // Arrange
        var email1 = EmailAddress.Of("test@example.com");
        var email2 = EmailAddress.Of("test@example.com");
        var email3 = EmailAddress.Of("other@example.com");

        // Assert
        email1.Equals(email2).ShouldBeTrue();
        email1.Equals(email3).ShouldBeFalse();
    }
}
