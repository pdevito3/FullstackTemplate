namespace FullstackTemplate.Server.Resources;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptions;
using MediatR;

/// <summary>
/// Runs the DataAnnotations rules of each MediatR request before its handler, for every entrypoint. The rules are on
/// the properties of the request and of each request property whose type is in this assembly, for example the
/// <c>Dto</c> of a command or the parameters DTO of a list query. A broken rule throws a
/// <see cref="RequestValidationException"/>. Each error has the camelCase JSON name of its field as the key, and that
/// name is also the field name in the message, for example "The field pageNumber must be between 1 and 4294967."
/// </summary>
public sealed class RequestValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        foreach (var rule in RequestRules.For(request.GetType()))
        {
            var value = rule.Property.GetValue(request);
            Validate(request, rule, value, errors);
            if (value is not null && RequestRules.IsRequestDto(rule.Property.PropertyType))
            {
                foreach (var dtoRule in RequestRules.For(value.GetType()))
                    Validate(value, dtoRule, dtoRule.Property.GetValue(value), errors);
            }
        }

        if (errors.Count > 0)
            throw new RequestValidationException(errors);

        return await next(cancellationToken);
    }

    private static void Validate(object instance, PropertyRule rule, object? value, Dictionary<string, string[]> errors)
    {
        if (rule.Attributes.Length == 0)
            return;

        var context = new ValidationContext(instance) { MemberName = rule.Property.Name, DisplayName = rule.JsonName };
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateValue(value, context, results, rule.Attributes))
            errors[rule.JsonName] = [.. results.Select(result => result.ErrorMessage ?? "The value is not valid.")];
    }
}

/// <summary>A property of a request type, with its DataAnnotations rules and its JSON name.</summary>
internal sealed record PropertyRule(PropertyInfo Property, ValidationAttribute[] Attributes, string JsonName);

internal static class RequestRules
{
    private static readonly ConcurrentDictionary<Type, PropertyRule[]> Rules = new();

    public static PropertyRule[] For(Type type) => Rules.GetOrAdd(type, static type => type
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.GetIndexParameters().Length == 0 && property.GetMethod is not null)
        .Select(property => new PropertyRule(
            property,
            [.. property.GetCustomAttributes<ValidationAttribute>(inherit: true)],
            property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name)))
        .ToArray());

    /// <summary>A DTO of this assembly, not a string, a collection, or a framework type.</summary>
    public static bool IsRequestDto(Type type) =>
        type.IsClass && type != typeof(string) && type.Assembly == typeof(RequestRules).Assembly;
}
