namespace FullstackTemplate.FunctionalTests.FunctionalTests.Telemetry;

using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using FullstackTemplate.Server.Domain.Tenants.Grpc.V1;
using global::Google.Protobuf;
using Serilog.Events;
using Shouldly;
using TestUtilities;

/// <summary>
/// Grpc.AspNetCore.Server logs a fault of a call at Information: an error status, a message that it cannot read, a
/// content type that is not gRPC, and a method that the Server does not have. Each log stays, so that an operator sees
/// the fault. The log of an error status does not hold its detail, because the detail can hold text that the caller typed.
/// </summary>
public class GrpcLogTests : TestBase
{
    private const string CallHandler = "Grpc.AspNetCore.Server.ServerCallHandler";
    private const string ListTenantsPath = "/fullstacktemplate.tenants.v1.TenantsService/ListTenants";

    [Test]
    public async Task an_error_status_is_logged_with_its_code_and_not_its_detail()
    {
        // Arrange
        SignInAsTrustedMachineClient();
        var email = $"jane{Guid.NewGuid():N}@example.com";
        var body = Frame(new ListTenantsRequest { SortOrder = email }.ToByteArray());

        // Act
        var (response, logs) = await SendAsync(ListTenantsPath, "application/grpc", body);

        // Assert
        GrpcStatus(response).ShouldBe("3");
        var status = logs.Where(e => From(e, CallHandler) && EventId(e) == 7).ShouldHaveSingleItem();
        status.Properties["StatusCode"].ToString().ShouldBe("InvalidArgument");
        TestingWebApplicationFactory.Instance.CapturedLogs.Holding(email).ShouldBeEmpty();
    }

    [Test]
    public async Task a_message_that_cannot_be_read_is_logged()
    {
        // Arrange
        SignInAsTrustedMachineClient();

        // Act
        // Field 3 says that 127 bytes follow, and only 1 byte follows
        var (_, logs) = await SendAsync(ListTenantsPath, "application/grpc", Frame([0x1a, 0x7f, 0x41]));

        // Assert
        logs.ShouldContain(e => From(e, CallHandler) && EventId(e) == 14);
    }

    [Test]
    public async Task a_content_type_that_is_not_grpc_is_logged()
    {
        // Arrange
        SignInAsTrustedMachineClient();

        // Act
        var (response, logs) = await SendAsync(ListTenantsPath, "text/plain", Frame([]));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        logs.ShouldContain(e => From(e, CallHandler) && e.MessageTemplate.Text.Contains("content-type"));
    }

    [Test]
    public async Task a_method_that_the_server_does_not_have_is_logged()
    {
        // Arrange
        SignInAsTrustedMachineClient();

        // Act
        var (_, logs) = await SendAsync("/fullstacktemplate.tenants.v1.TenantsService/NoSuchMethod", "application/grpc", Frame([]));

        // Assert
        logs.ShouldContain(e => From(e, "Grpc.") && e.MessageTemplate.Text.Contains("nimplemented"));
    }

    private static bool From(LogEvent logEvent, string sourceContext) =>
        logEvent.Properties.TryGetValue("SourceContext", out var value)
        && value is ScalarValue { Value: string source } && source.StartsWith(sourceContext, StringComparison.Ordinal);

    // Microsoft.Extensions.Logging gives Serilog the event ID as a structure with an Id and a Name
    private static int? EventId(LogEvent logEvent) =>
        logEvent.Properties.TryGetValue("EventId", out var value) && value is StructureValue structure
            && structure.Properties.FirstOrDefault(p => p.Name == "Id")?.Value is ScalarValue { Value: int id }
            ? id
            : null;

    // A call that fails before its first message gets the status in the headers, with no trailers
    private static string? GrpcStatus(HttpResponseMessage response) =>
        (response.TrailingHeaders.TryGetValues("grpc-status", out var trailer) ? trailer
            : response.Headers.TryGetValues("grpc-status", out var header) ? header : []).SingleOrDefault();

    // A gRPC message frame: no compression, the length in 4 bytes big-endian, then the message
    private static byte[] Frame(byte[] message)
    {
        var frame = new byte[5 + message.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(1), (uint)message.Length);
        message.CopyTo(frame.AsSpan(5));
        return frame;
    }

    private async Task<(HttpResponseMessage Response, IReadOnlyList<LogEvent> Logs)> SendAsync(
        string path, string contentType, byte[] body)
    {
        var userAgent = $"grpc-log-test/{Guid.NewGuid():N}";
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        request.Headers.TryAddWithoutValidation("TE", "trailers");
        request.Headers.UserAgent.ParseAdd(userAgent);

        var response = await FactoryClient.SendAsync(request);
        await response.Content.ReadAsByteArrayAsync();

        var captured = TestingWebApplicationFactory.Instance.CapturedLogs;
        var deadline = DateTime.UtcNow.AddSeconds(5);
        LogEvent? requestLog;
        while ((requestLog = captured.WithUserAgent(userAgent).FirstOrDefault(e => e.Properties.ContainsKey("StatusCode"))) is null)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"No request log for {userAgent}.");
            await Task.Delay(20);
        }
        return (response, captured.InTrace(requestLog.TraceId.ShouldNotBeNull()));
    }

    private void SignInAsTrustedMachineClient()
        => FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });
}
