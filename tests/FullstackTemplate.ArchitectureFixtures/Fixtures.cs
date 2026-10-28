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
    using FullstackTemplate.Server.Domain.Users.Features;
    using FullstackTemplate.Server.Resources;
    using MediatR;
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

    // Not rule 4: the copy method of the record creates the request itself
    public sealed record CleanRunAgainCommand(int Value) : IRequest<int>, IRunsAgainWhenRaced;

    // Rule 4: a factory method on the request lets a handler create it
    public sealed record RunAgainCommandWithFactory(int Value) : IRequest<int>, IRunsAgainWhenRaced
    {
        public static RunAgainCommandWithFactory For(int value) => new(value);
    }

    // Rule 4: core creates a command of the Server that runs again when raced
    public static class CoreWithRunAgainCommand
    {
        public static object Create() => new AddUserPermission.Command("usr_fixture", "read_users");
    }

    // Rule 4: core creates a command that runs again only in the state machine of an async method
    public static class CoreWithAsyncRunAgainCommand
    {
        public static async Task<int> Run(ISender sender)
        {
            await Task.Yield();
            return await sender.Send(new CleanRunAgainCommand(1));
        }
    }

    // Not rule 4: a struct command that its own code does not create
    public readonly record struct RunAgainStructCommand(int Value) : IRequest<int>, IRunsAgainWhenRaced;

    // Rule 4: core builds a struct command in place, with call .ctor
    public static class CoreWithRunAgainStructInPlace
    {
        public static int Create()
        {
            var command = new RunAgainStructCommand(1);
            return command.Value;
        }
    }

    // Rule 4: core builds a struct command with initobj
    public static class CoreWithRunAgainStructOfDefault
    {
        public static int Create()
        {
            RunAgainStructCommand command = default;
            return command.Value;
        }
    }

    // RunAgainRequestTests: a version on the command, in a DTO of the command, and in a list of DTOs
    public sealed record RunAgainCommandWithVersion(string Id, uint Version) : IRunsAgainWhenRaced;

    public sealed record RunAgainCommandWithVersionInDto(string Id, VersionedBody Body) : IRunsAgainWhenRaced;

    public sealed record RunAgainCommandWithVersionInDtoList(IReadOnlyList<VersionedBody> Lines) : IRunsAgainWhenRaced;

    public readonly record struct RunAgainStructCommandWithVersion(string Id, uint Version) : IRunsAgainWhenRaced;

    public readonly record struct RunAgainStructCommandWithVersionInDto(string Id, VersionedStructBody Body)
        : IRunsAgainWhenRaced;

    public readonly record struct VersionedStructBody(string Name, uint Version);

    public sealed class VersionedBody
    {
        public string Name { get; init; } = string.Empty;
        public uint Version { get; init; }
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

    // Not rule 4: an entrypoint sends a command that runs again
    public sealed class CleanRunAgainEndpoint : IEndpoint
    {
        public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup.MapPost("/run-again",
            (MediatR.ISender sender) => sender.Send(new Core.CleanRunAgainCommand(1)));
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
