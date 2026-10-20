namespace FullstackTemplate.UnitTests.Resources;

using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources.Entrypoints.Grpc;
using Google.Rpc;
using Grpc.Core;
using NSubstitute;
using Shouldly;

public class DomainErrorInterceptorTests
{
    private static readonly NotFoundException NotFound = new("Tenant was not found.");
    private readonly DomainErrorInterceptor _interceptor = new();
    private readonly ServerCallContext _context = Substitute.For<ServerCallContext>();
    private readonly IAsyncStreamReader<string> _requests = Substitute.For<IAsyncStreamReader<string>>();
    private readonly IServerStreamWriter<string> _responses = Substitute.For<IServerStreamWriter<string>>();

    [Test]
    public async Task a_unary_call_maps_the_domain_error()
    {
        var exception = await Should.ThrowAsync<RpcException>(() => _interceptor.UnaryServerHandler<string, string>(
            "request", _context, (_, _) => throw NotFound));

        ShouldBeNotFound(exception);
    }

    [Test]
    public async Task a_server_streaming_call_maps_the_domain_error()
    {
        var exception = await Should.ThrowAsync<RpcException>(() => _interceptor.ServerStreamingServerHandler<string, string>(
            "request", _responses, _context, (_, _, _) => throw NotFound));

        ShouldBeNotFound(exception);
    }

    [Test]
    public async Task a_client_streaming_call_maps_the_domain_error()
    {
        var exception = await Should.ThrowAsync<RpcException>(() => _interceptor.ClientStreamingServerHandler<string, string>(
            _requests, _context, (_, _) => throw NotFound));

        ShouldBeNotFound(exception);
    }

    [Test]
    public async Task a_duplex_streaming_call_maps_the_domain_error()
    {
        var exception = await Should.ThrowAsync<RpcException>(() => _interceptor.DuplexStreamingServerHandler<string, string>(
            _requests, _responses, _context, (_, _, _) => throw NotFound));

        ShouldBeNotFound(exception);
    }

    [Test]
    public async Task a_field_error_gives_a_bad_request_detail_with_each_message()
    {
        var invalid = new RequestValidationException(new Dictionary<string, string[]>
        {
            ["name"] = ["The name field is required."],
            ["pageSize"] = ["The pageSize must be 1 or more.", "The pageSize is too large."],
        });

        var exception = await Should.ThrowAsync<RpcException>(() => _interceptor.UnaryServerHandler<string, string>(
            "request", _context, (_, _) => throw invalid));

        exception.StatusCode.ShouldBe(StatusCode.InvalidArgument);
        var violations = exception.GetRpcStatus().ShouldNotBeNull().GetDetail<BadRequest>().ShouldNotBeNull().FieldViolations;
        violations.Select(v => (v.Field, v.Description)).ShouldBe([
            ("name", "The name field is required."),
            ("pageSize", "The pageSize must be 1 or more."),
            ("pageSize", "The pageSize is too large."),
        ]);
    }

    [Test]
    public async Task an_error_with_no_fields_gives_no_bad_request_detail()
    {
        var exception = await Should.ThrowAsync<RpcException>(() => _interceptor.UnaryServerHandler<string, string>(
            "request", _context, (_, _) => throw NotFound));

        exception.GetRpcStatus().ShouldNotBeNull().GetDetail<BadRequest>().ShouldBeNull();
    }

    [Test]
    public async Task a_server_fault_is_not_mapped()
    {
        await Should.ThrowAsync<InvalidOperationException>(() => _interceptor.ServerStreamingServerHandler<string, string>(
            "request", _responses, _context, (_, _, _) => throw new InvalidOperationException("secret")));
    }

    private static void ShouldBeNotFound(RpcException exception)
    {
        exception.StatusCode.ShouldBe(StatusCode.NotFound);
        exception.Status.Detail.ShouldBe("Tenant was not found.");
    }
}
