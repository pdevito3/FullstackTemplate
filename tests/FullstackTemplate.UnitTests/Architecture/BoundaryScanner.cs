namespace FullstackTemplate.UnitTests.Architecture;

using System.CodeDom.Compiler;
using System.Reflection;
using System.Text.RegularExpressions;
using FullstackTemplate.Server.Resources.Entrypoints.Http;
using global::Grpc.Core;
using ModelContextProtocol.Server;
using Mono.Cecil;
using Mono.Cecil.Cil;

/// <summary>
/// Sorts the types of one assembly into core, adapters, the shared plumbing, and the gRPC contract, and finds the
/// types that break a boundary rule. The rules read the IL with Mono.Cecil, so they see a dependency in a method
/// body, an attribute argument, or a generic constraint, not only in a signature. A nested type, such as the state
/// machine of an async method, belongs to its outermost type, so a result names only top-level types.
/// <list type="bullet">
/// <item>An adapter is an <see cref="IEndpoint"/>, an <see cref="McpServerToolTypeAttribute"/> class, a gRPC service
/// (a class whose base has <see cref="BindServiceMethodAttribute"/>), or a hand-written type in a
/// <c>Grpc.V{n}</c> namespace, such as a map from a DTO to a message.</item>
/// <item>The shared plumbing is the <c>Resources.Entrypoints</c> namespaces under the root namespace.</item>
/// <item>The gRPC contract is the code that protoc generates in a <c>Grpc.V{n}</c> namespace.</item>
/// <item>The composition root wires the core to the entrypoints, so it sees both: <c>Program</c> in the global
/// namespace, and the types that the scope names.</item>
/// <item>Each other type under the root namespace is core.</item>
/// </list>
/// </summary>
public sealed partial class BoundaryScanner
{
    /// <summary>The namespaces of the transport libraries. Only an adapter or the shared plumbing can use them.</summary>
    public static readonly string[] TransportNamespaces =
    [
        "Microsoft.AspNetCore.Http",
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.AspNetCore.OpenApi",
        "Microsoft.AspNetCore.Routing",
        "Microsoft.OpenApi",
        "Asp.Versioning",
        "Swashbuckle",
        "Grpc",
        "Google.Protobuf",
        "Google.Rpc",
        "ModelContextProtocol",
    ];

    private readonly Assembly _assembly;
    private readonly string _rootNamespace;
    private readonly string _sharedPlumbing;
    private readonly HashSet<string> _compositionRoot;
    private readonly HashSet<string> _allowedTransportTypes;
    private readonly ModuleDefinition _module;

    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="rootNamespace">The root namespace of the assembly. A type outside it is not core.</param>
    /// <param name="compositionRoot">The full names of the types that wire the host, outside the global namespace.</param>
    /// <param name="allowedTransportTypes">The full names of the transport types that core can use.</param>
    public BoundaryScanner(
        Assembly assembly,
        string rootNamespace,
        IEnumerable<string>? compositionRoot = null,
        IEnumerable<string>? allowedTransportTypes = null)
    {
        _assembly = assembly;
        _rootNamespace = rootNamespace;
        _sharedPlumbing = rootNamespace + ".Resources.Entrypoints";
        _compositionRoot = (compositionRoot ?? []).ToHashSet(StringComparer.Ordinal);
        _allowedTransportTypes = (allowedTransportTypes ?? []).ToHashSet(StringComparer.Ordinal);
        // Cecil reads the method bodies on demand, so the module stays open. It reads from memory, so no file stays open.
        _module = ModuleDefinition.ReadModule(new MemoryStream(File.ReadAllBytes(assembly.Location)));
    }

    /// <summary>The database. Only a handler or a service can use it, so an adapter must send a MediatR request.</summary>
    public string[] DatabaseNamespaces =>
    [
        _rootNamespace + ".Databases",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
    ];

    [GeneratedRegex(@"\.Grpc\.V\d+$")]
    private static partial Regex GrpcContractNamespace();

    /// <summary>Rule 1: the core types that use a transport library.</summary>
    public string[] CoreThatUsesATransport()
        => TypesWithDependencyOnAny(CoreTypes(), TransportNamespaces, _allowedTransportTypes);

    /// <summary>Rule 2: the core types that use an adapter, the shared plumbing, or the gRPC contract.</summary>
    public string[] CoreThatUsesAnEntrypoint()
    {
        var entrypoints = AdapterNames()
            .Concat(GrpcContractNamespaces())
            .Append(_sharedPlumbing)
            .ToArray();
        return TypesWithDependencyOnAny(CoreTypes(), entrypoints, []);
    }

    /// <summary>Rule 3: the adapters and the shared plumbing that use the database.</summary>
    public string[] AdaptersThatUseTheDatabase()
    {
        var adapterNames = AdapterNames();
        var adaptersAndPlumbing = AllTypes().Where(t =>
            adapterNames.Contains(Outermost(t).FullName) || IsSharedPlumbing(Outermost(t)));
        return TypesWithDependencyOnAny(adaptersAndPlumbing, DatabaseNamespaces, []);
    }

    /// <summary>The top-level adapter types.</summary>
    public HashSet<string> AdapterNames()
        => AllTypes()
            .Where(t => t.DeclaringType is null && IsAdapter(t))
            .Select(t => t.FullName)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>The top-level core types.</summary>
    public HashSet<string> CoreNames()
        => CoreTypes().Select(t => Outermost(t).FullName).ToHashSet(StringComparer.Ordinal);

    /// <summary>The namespaces that hold the generated gRPC contract.</summary>
    public string[] GrpcContractNamespaces()
        => AllTypes()
            .Where(t => t.DeclaringType is null && IsGrpcContract(t))
            .Select(t => t.Namespace)
            .Distinct()
            .ToArray();

    private bool IsAdapter(TypeDefinition type)
    {
        var reflected = _assembly.GetType(type.FullName.Replace('/', '+'));
        if (reflected is null || !reflected.IsClass)
            return false;
        return typeof(IEndpoint).IsAssignableFrom(reflected)
            || reflected.IsDefined(typeof(McpServerToolTypeAttribute))
            || reflected.BaseType?.IsDefined(typeof(BindServiceMethodAttribute)) == true
            || GrpcContractNamespace().IsMatch(type.Namespace) && !IsGrpcContract(type);
    }

    /// <summary>
    /// protoc marks the members of the code that it generates with <see cref="GeneratedCodeAttribute"/>. The
    /// reflection class of a proto file has no mark, but it holds the <c>FileDescriptor</c> of the file.
    /// </summary>
    private static bool IsGrpcContract(TypeDefinition type)
    {
        if (!GrpcContractNamespace().IsMatch(type.Namespace))
            return false;
        return HasGeneratedCode(type)
            || type.Properties.Any(p => p.Name == "Descriptor"
                && p.PropertyType.FullName == "Google.Protobuf.Reflection.FileDescriptor");
    }

    private static bool HasGeneratedCode(TypeDefinition type)
    {
        static bool Marked(Mono.Cecil.ICustomAttributeProvider provider)
            => provider.CustomAttributes.Any(a => a.AttributeType.FullName == typeof(GeneratedCodeAttribute).FullName);
        return Marked(type)
            || type.Methods.Any(Marked)
            || type.Fields.Any(Marked)
            || type.Properties.Any(Marked)
            || type.NestedTypes.Any(HasGeneratedCode);
    }

    private bool IsSharedPlumbing(TypeDefinition outer)
        => outer.Namespace == _sharedPlumbing || outer.Namespace.StartsWith(_sharedPlumbing + ".", StringComparison.Ordinal);

    private IEnumerable<TypeDefinition> CoreTypes()
    {
        var adapterNames = AdapterNames();
        return AllTypes().Where(type =>
        {
            var outer = Outermost(type);
            return (outer.Namespace == _rootNamespace || outer.Namespace.StartsWith(_rootNamespace + ".", StringComparison.Ordinal))
                && !IsSharedPlumbing(outer)
                && !IsGrpcContract(outer)
                && !adapterNames.Contains(outer.FullName)
                && !_compositionRoot.Contains(outer.FullName);
        });
    }

    /// <summary>
    /// The top-level types that use a type whose full name starts with one of the prefixes, for example a namespace,
    /// and that is not in the allowed set.
    /// </summary>
    private static string[] TypesWithDependencyOnAny(
        IEnumerable<TypeDefinition> types, string[] prefixes, HashSet<string> allowed)
        => types
            .Where(type => ReferencedTypes(type).Any(name => !allowed.Contains(name) && prefixes.Any(prefix =>
                name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal)
                || name.StartsWith(prefix + "/", StringComparison.Ordinal)
                || name.StartsWith(prefix + "`", StringComparison.Ordinal))))
            .Select(type => Outermost(type).FullName)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// The full names of the types in the signatures, the attributes, the generic constraints, and the method bodies
    /// of a type. A generic argument names its own type, so <c>List`1&lt;IResult&gt;</c> also gives <c>IResult</c>.
    /// </summary>
    private static IEnumerable<string> ReferencedTypes(TypeDefinition type)
    {
        var found = new List<TypeReference?> { type.BaseType };
        found.AddRange(type.Interfaces.Select(i => i.InterfaceType));
        found.AddRange(AttributeTypes(type));
        found.AddRange(ConstraintTypes(type));
        foreach (var field in type.Fields)
        {
            found.Add(field.FieldType);
            found.AddRange(AttributeTypes(field));
        }
        foreach (var property in type.Properties)
        {
            found.Add(property.PropertyType);
            found.AddRange(AttributeTypes(property));
        }
        foreach (var @event in type.Events)
        {
            found.Add(@event.EventType);
            found.AddRange(AttributeTypes(@event));
        }
        foreach (var method in type.Methods)
        {
            found.Add(method.ReturnType);
            found.AddRange(AttributeTypes(method));
            found.AddRange(AttributeTypes(method.MethodReturnType));
            found.AddRange(ConstraintTypes(method));
            foreach (var parameter in method.Parameters)
            {
                found.Add(parameter.ParameterType);
                found.AddRange(AttributeTypes(parameter));
            }
            if (!method.HasBody)
                continue;
            found.AddRange(method.Body.Variables.Select(v => v.VariableType));
            foreach (var instruction in method.Body.Instructions)
                found.AddRange(OperandTypes(instruction));
        }
        return found.OfType<TypeReference>().SelectMany(Flatten).Select(t => t.FullName).Distinct();
    }

    private static IEnumerable<TypeReference> ConstraintTypes(IGenericParameterProvider provider)
        => provider.GenericParameters.SelectMany(parameter => parameter.Constraints
            .Select(c => c.ConstraintType)
            .Concat(AttributeTypes(parameter)));

    /// <summary>The type of each attribute, and each type in its constructor arguments and its named arguments.</summary>
    private static IEnumerable<TypeReference> AttributeTypes(Mono.Cecil.ICustomAttributeProvider provider)
        => provider.CustomAttributes.SelectMany(attribute => attribute.ConstructorArguments
            .Concat(attribute.Properties.Select(p => p.Argument))
            .Concat(attribute.Fields.Select(f => f.Argument))
            .SelectMany(ArgumentTypes)
            .Prepend(attribute.AttributeType));

    // A typeof argument is a TypeReference. A params or array argument holds more arguments
    private static IEnumerable<TypeReference> ArgumentTypes(CustomAttributeArgument argument) => argument.Value switch
    {
        TypeReference type => [type, argument.Type],
        CustomAttributeArgument inner => ArgumentTypes(inner).Append(argument.Type),
        CustomAttributeArgument[] items => items.SelectMany(ArgumentTypes).Append(argument.Type),
        _ => [argument.Type],
    };

    private static IEnumerable<TypeReference?> OperandTypes(Instruction instruction) => instruction.Operand switch
    {
        TypeReference typeReference => [typeReference],
        MethodReference method => method.Parameters.Select(p => p.ParameterType)
            .Append(method.DeclaringType)
            .Append(method.ReturnType)
            .Concat(method is GenericInstanceMethod generic ? generic.GenericArguments : []),
        FieldReference field => [field.DeclaringType, field.FieldType],
        _ => [],
    };

    // A generic instance, an array, or a by-ref type also uses its element type and its generic arguments
    private static IEnumerable<TypeReference> Flatten(TypeReference type)
    {
        yield return type;
        if (type is TypeSpecification specification)
            foreach (var inner in Flatten(specification.ElementType))
                yield return inner;
        if (type is GenericInstanceType generic)
            foreach (var inner in generic.GenericArguments.SelectMany(Flatten))
                yield return inner;
    }

    /// <summary>Each type of the assembly, also each nested type and each type that the compiler generates.</summary>
    private IEnumerable<TypeDefinition> AllTypes() => _module.GetTypes();

    private static TypeDefinition Outermost(TypeDefinition type)
    {
        while (type.DeclaringType is not null)
            type = type.DeclaringType;
        return type;
    }
}
