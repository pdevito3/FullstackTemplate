namespace FullstackTemplate.Server.Resources.Entrypoints.Grpc;

// "Grpc" alone would bind to this namespace, so the gRPC library needs the global alias.
using global::Google.Protobuf.WellKnownTypes;
using global::Google.Rpc;
using global::Grpc.Core;
using global::Grpc.Core.Interceptors;
using Exceptions;
using Status = global::Google.Rpc.Status;

/// <summary>
/// Maps a domain exception to a gRPC status, the same as <see cref="DomainProblemDetails"/> maps it to
/// an HTTP status. When the error names request fields, the status has a <c>google.rpc.BadRequest</c> detail with one
/// field violation for each message, the same fields as the <c>errors</c> of an HTTP problem. A server fault is not mapped: gRPC returns UNKNOWN with no detail, so the message stays on the server.
/// The interceptor handles each kind of call: unary, server streaming, client streaming, and duplex streaming.
/// </summary>
public sealed class DomainErrorInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (Exception exception) when (ToRpcException(exception) is { } rpcException)
        {
            throw rpcException;
        }
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(requestStream, context);
        }
        catch (Exception exception) when (ToRpcException(exception) is { } rpcException)
        {
            throw rpcException;
        }
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            await continuation(request, responseStream, context);
        }
        catch (Exception exception) when (ToRpcException(exception) is { } rpcException)
        {
            throw rpcException;
        }
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            await continuation(requestStream, responseStream, context);
        }
        catch (Exception exception) when (ToRpcException(exception) is { } rpcException)
        {
            throw rpcException;
        }
    }

    public static StatusCode? StatusCodeFor(DomainErrorKind kind) => kind switch
    {
        DomainErrorKind.Validation or DomainErrorKind.InvalidInput => StatusCode.InvalidArgument,
        DomainErrorKind.NotFound => StatusCode.NotFound,
        DomainErrorKind.Forbidden => StatusCode.PermissionDenied,
        DomainErrorKind.Conflict => StatusCode.AlreadyExists,
        _ => null,
    };

    private static RpcException? ToRpcException(Exception exception)
    {
        var error = DomainError.From(exception);
        if (StatusCodeFor(error.Kind) is not { } code)
            return null;

        var status = new Status { Code = (int)code, Message = error.Detail ?? error.Title };
        if (error.Errors is { Count: > 0 } errors)
        {
            var badRequest = new BadRequest();
            badRequest.FieldViolations.AddRange(errors.SelectMany(field => field.Value.Select(message =>
                new BadRequest.Types.FieldViolation { Field = field.Key, Description = message })));
            status.Details.Add(Any.Pack(badRequest));
        }
        return status.ToRpcException();
    }
}
