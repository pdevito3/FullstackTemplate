namespace FullstackTemplate.Server.Domain.EmailAddresses;

using FluentValidation;

public sealed partial class EmailAddress : ValueObject
{
    public string Value { get; private set; } = default!;

    public EmailAddress(string value)
    {
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(value, "Please provide a valid email.");
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
            RuleFor(email => email).EmailAddress();
        }
    }
}