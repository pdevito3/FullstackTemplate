namespace FullstackTemplate.Server.Resources;

using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptions;
using MediatR;

/// <summary>
/// Runs the DataAnnotations rules of each MediatR request before its handler, for every entrypoint. The rules are on
/// the properties of the request and of each DTO in the request, at any depth. A DTO is a property whose type is a
/// class of the assembly of its owner, for example the <c>Dto</c> of a command, or an element of a list of these
/// classes. A broken rule throws a <see cref="RequestValidationException"/>. The key of a field of the request or of its
/// body DTO is the camelCase JSON name of the field. The key of a field of a DTO below them has the path of the field,
/// for example "shipTo.city" or "lines[1].name". The message names only the JSON name of the field, for example "The
/// field pageNumber must be between 1 and 4294967." or "The name field is required." under "lines[1].name".
/// </summary>
[SuppressMessage("Types", "FSTTYP004:A signature takes or returns object",
    Justification = "DataAnnotations boundary. The walker validates each DTO type that a request holds, and Validator takes object.")]
public sealed class RequestValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var rule in RequestRules.For(request.GetType()))
        {
            var value = rule.Read(request);
            Validate(request, rule, value, rule.JsonName, errors);
            if (rule.Holds == PropertyHolds.Dto && value is not null)
                ValidateDto(value, "", errors, visited);
            else
                ValidateChildren(rule, value, rule.JsonName, errors, visited);
        }

        if (errors.Count > 0)
            throw new RequestValidationException(errors);

        return await next(cancellationToken);
    }

    private static void ValidateDto(
        object dto, string prefix, Dictionary<string, string[]> errors, HashSet<object> visited)
    {
        if (!visited.Add(dto))
            return;

        foreach (var rule in RequestRules.For(dto.GetType()))
        {
            var value = rule.Read(dto);
            var key = prefix + rule.JsonName;
            Validate(dto, rule, value, key, errors);
            ValidateChildren(rule, value, key, errors, visited);
        }
    }

    private static void ValidateChildren(
        PropertyRule rule, object? value, string key, Dictionary<string, string[]> errors, HashSet<object> visited)
    {
        switch (rule.Holds)
        {
            case PropertyHolds.Dto when value is not null:
                ValidateDto(value, $"{key}.", errors, visited);
                break;
            case PropertyHolds.DtoList when value is IEnumerable elements:
                var index = 0;
                foreach (var element in elements)
                {
                    if (element is not null)
                        ValidateDto(element, $"{key}[{index}].", errors, visited);
                    index++;
                }
                break;
        }
    }

    private static void Validate(
        object instance, PropertyRule rule, object? value, string key, Dictionary<string, string[]> errors)
    {
        if (rule.Attributes.Length == 0)
            return;

        var context = new ValidationContext(instance) { MemberName = rule.Property.Name, DisplayName = rule.JsonName };
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateValue(value, context, results, rule.Attributes))
            errors[key] = [.. results.Select(result => result.ErrorMessage ?? "The value is not valid.")];
    }
}

internal enum PropertyHolds
{
    Value,
    Dto,
    DtoList,
}

/// <summary>
/// The rules of one property. <c>Read</c> is a compiled getter of the property, so a request reads no value with
/// reflection.
/// </summary>
internal sealed record PropertyRule(
    PropertyInfo Property, Func<object, object?> Read, ValidationAttribute[] Attributes, string JsonName, PropertyHolds Holds);

internal static class RequestRules
{
    private static readonly ConcurrentDictionary<Type, PropertyRule[]> Rules = new();

    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
        Justification = "Per-type cache. The rules of a request type are read one time, then come from Rules.")]
    public static PropertyRule[] For(Type type) => Rules.GetOrAdd(type, static type => type
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.GetIndexParameters().Length == 0 && property.GetMethod is not null)
        .Select(property => new PropertyRule(
            property,
            ReaderOf(type, property),
            [.. property.GetCustomAttributes<ValidationAttribute>(inherit: true)],
            property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name),
            HoldsOf(property.PropertyType, type.Assembly)))
        .ToArray());

    /// <summary>instance => (object?)((Type)instance).Property, compiled one time for each property of a type.</summary>
    private static Func<object, object?> ReaderOf(Type type, PropertyInfo property)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var value = Expression.Property(Expression.Convert(instance, type), property);
        return Expression.Lambda<Func<object, object?>>(Expression.Convert(value, typeof(object)), instance).Compile();
    }

    private static PropertyHolds HoldsOf(Type type, Assembly owner)
    {
        var element = type.GetInterfaces().Append(type)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
        if (element is not null && IsDto(element, owner))
            return PropertyHolds.DtoList;

        return IsDto(type, owner) ? PropertyHolds.Dto : PropertyHolds.Value;
    }

    // The runtime gives an array type the assembly of its element, so an array of DTOs passes the assembly check.
    private static bool IsDto(Type type, Assembly owner) =>
        type.IsClass && !type.IsArray && type != typeof(string) && type.Assembly == owner;
}
