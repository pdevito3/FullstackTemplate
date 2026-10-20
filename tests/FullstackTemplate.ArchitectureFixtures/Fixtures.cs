// Each type in this file is a fixture for EntrypointBoundaryTests. A type named "...With..." breaks one rule in one
// way. A type named "Clean..." breaks no rule. Do not fix the violations.

namespace FullstackTemplate.ArchitectureFixtures
{
    public static class FixtureMarker
    {
        public const string RootNamespace = "FullstackTemplate.ArchitectureFixtures";

        /// <summary>The one transport type that the scan of the fixtures allows in core.</summary>
        public const string AllowedTransportType = "Microsoft.AspNetCore.Http.PathString";
    }
}

namespace FullstackTemplate.ArchitectureFixtures.Databases
{
    public sealed class FixtureDb
    {
        public int Count { get; set; }
    }
}

namespace FullstackTemplate.ArchitectureFixtures.Core
{
    using Adapters;
    using Contract.Grpc.V1;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Resources.Entrypoints;

    [AttributeUsage(AttributeTargets.All)]
    public sealed class MarkerAttribute : Attribute
    {
        public MarkerAttribute() { }
        public MarkerAttribute(Type type) => Type = type;
        public Type? Type { get; set; }
    }

    public sealed class CleanCore
    {
        public int Value { get; set; }
    }

    // Rule 1: a transport type in a signature and a method body
    public static class CoreWithHttpResult
    {
        public static IResult Ok() => Results.Ok();
    }

    // Rule 1: a transport type only in a named attribute argument
    [Marker(Type = typeof(ProblemDetails))]
    public sealed class CoreWithNamedAttributeArgument;

    // Rule 1: a transport type only in an attribute on a return value
    public static class CoreWithReturnAttribute
    {
        [return: Marker(typeof(ProblemDetails))]
        public static int Get() => 0;
    }

    // Rule 1: a transport type only in a generic constraint of a type
    public sealed class CoreWithGenericConstraint<T> where T : IResult;

    // Rule 1: a transport type only in a generic constraint of a method
    public static class CoreWithMethodGenericConstraint
    {
        public static void Run<T>() where T : IResult { }
    }

    // Rule 1: a transport type only in the state machine of an async method
    public static class CoreWithAsyncTransportUse
    {
        public static async Task<string> Run()
        {
            await Task.Yield();
            return new ProblemDetails().Title ?? string.Empty;
        }
    }

    // Not rule 1: the scan of the fixtures allows PathString
    public sealed class CoreWithAllowedTransportType
    {
        public PathString Path;
    }

    // Rule 2: core uses an adapter
    public static class CoreWithAdapter
    {
        public static Type Get() => typeof(CleanEndpoint);
    }

    // Rule 2: core uses the gRPC contract
    public static class CoreWithGrpcContract
    {
        public static GeneratedMessage Create() => new();
    }

    // Rule 2: core uses the shared plumbing
    public static class CoreWithSharedPlumbing
    {
        public static int Get() => Plumbing.Value;
    }
}

namespace FullstackTemplate.ArchitectureFixtures.Resources.Entrypoints
{
    using Databases;

    public static class Plumbing
    {
        public static readonly int Value = 1;
    }

    // Rule 3: the shared plumbing uses the database
    public static class PlumbingWithDatabase
    {
        public static int Count(FixtureDb db) => db.Count;
    }
}

namespace FullstackTemplate.ArchitectureFixtures.Contract.Grpc.V1
{
    using System.CodeDom.Compiler;
    using Databases;
    using global::Grpc.Core;

    // The contract: protoc marks each member with GeneratedCode. The contract can use the transport libraries.
    public sealed class GeneratedMessage
    {
        [GeneratedCode("protoc", null)]
        public global::Google.Protobuf.ByteString Data { get; set; } = global::Google.Protobuf.ByteString.Empty;
    }

    [BindServiceMethod(typeof(GeneratedServiceBase), "BindService")]
    public abstract class GeneratedServiceBase
    {
        [GeneratedCode("grpc_csharp_plugin", null)]
        public virtual Task<GeneratedMessage> Get(GeneratedMessage request, ServerCallContext context)
            => Task.FromResult(request);
    }

    // Rule 3: a hand-written type in a gRPC namespace is an adapter, and it uses the database
    public static class HandWrittenGrpcMappings
    {
        public static GeneratedMessage ToMessage(FixtureDb db) => new() { Data = global::Google.Protobuf.ByteString.CopyFromUtf8(db.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)) };
    }
}

namespace FullstackTemplate.ArchitectureFixtures.Adapters
{
    using Contract.Grpc.V1;
    using Databases;
    using FullstackTemplate.Server.Resources.Entrypoints.Http;
    using global::Grpc.Core;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Routing;
    using ModelContextProtocol.Server;

    public sealed class CleanEndpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup.MapGet("/clean", () => "clean");
    }

    // Rule 3: an endpoint uses the database
    public sealed class EndpointWithDatabase : IEndpoint
    {
        public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup.MapGet("/db", () => new FixtureDb().Count);
    }

    // Rule 3: an MCP tool uses EF Core
    [McpServerToolType]
    public static class ToolWithDatabase
    {
        [McpServerTool(Name = "fixture")]
        public static string Run(Microsoft.EntityFrameworkCore.DbContext db) => db.GetType().Name;
    }

    // Rule 3: a gRPC service uses the database
    public sealed class GrpcServiceWithDatabase : GeneratedServiceBase
    {
        public override Task<GeneratedMessage> Get(GeneratedMessage request, ServerCallContext context)
            => Task.FromResult(HandWrittenGrpcMappings.ToMessage(new FixtureDb()));
    }
}
