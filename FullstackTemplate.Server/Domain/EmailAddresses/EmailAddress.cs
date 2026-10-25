namespace FullstackTemplate.Server.Domain.EmailAddresses;

using FluentValidation;

public sealed partial class EmailAddress : ValueObject
{
    public const int MaxLength = 320;

    public string Value { get; private set; } = default!;

    public EmailAddress(string value)
    {
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(value, "email", "Please provide a valid email.");
        Exceptions.ValidationException.ThrowWhenLongerThan(value, MaxLength, "email",
            $"An email can have no more than {MaxLength} characters.");
        Exceptions.ValidationException.ThrowWhenHasCharacterPostgresCannotStore(value, "email",
            "An email has a character that is not allowed.");
        new EmailValidator().ValidateAndThrow(value);
        Value = value;
    }

    public static EmailAddress Of(string value) => new EmailAddress(value);
    public static implicit operator string(EmailAddress value) => value.Value;

    private EmailAddress() { } // EF Core
    
    private sealed class EmailValidator : AbstractValidator<string> 
    {
        public EmailValidator()
        {
            // The key in the errors of the problem is the name of the request field
            RuleFor(email => email)
                .EmailAddress()
                .OverridePropertyName("email")
                .WithMessage("Please provide a valid email.");
        }
    }
}