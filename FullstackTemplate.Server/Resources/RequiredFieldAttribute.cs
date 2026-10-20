namespace FullstackTemplate.Server.Resources;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Marks a request field that must be in the body. An empty string is allowed, so that the domain rules can
/// reject it with a 422. <see cref="RequestValidationBehavior{TRequest,TResponse}"/> names the field with its
/// camelCase JSON name, for example "The firstName field is required."
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class RequiredFieldAttribute : RequiredAttribute
{
    public RequiredFieldAttribute() => AllowEmptyStrings = true;
}
