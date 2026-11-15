namespace FullstackTemplate.IntegrationTests.Resources.Storage;

using System.Globalization;
using System.Net;
using Amazon.S3;
using FullstackTemplate.Server.Resources.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

public class ObjectStorageClockTests
{
    [Test]
    public async Task a_storage_whose_clock_is_20_minutes_ahead_refuses_each_call_of_the_server()
    {
        // Arrange
        await using var storage = await SkewedStorage.StartAsync(TimeSpan.FromMinutes(20));
        await using var server = TestFixture.BuildServiceProvider(_ => { },
            settings: new Dictionary<string, string?> { ["AWS_ENDPOINT_URL"] = storage.Endpoint });

        // Act
        var list = () => server.GetRequiredService<ObjectStorage>().ListPageAsync("tenants/", null, CancellationToken.None);

        // Assert
        var refused = await list.ShouldThrowAsync<AmazonS3Exception>(
            "The sweep compares the time of an object with the clock of the Server, so the client must not sign with the clock of storage.");
        refused.ErrorCode.ShouldBe(SkewedStorage.TooSkewed);
    }

    /// <summary>
    /// An S3 whose clock is ahead of this machine. As S3 does, it gives its time in the Date header of each reply,
    /// refuses a request signed more than 15 minutes from its time, and lists no objects for any other request.
    /// </summary>
    private sealed class SkewedStorage(WebApplication app) : IAsyncDisposable
    {
        public const string TooSkewed = "RequestTimeTooSkewed";
        private static readonly TimeSpan LargestSkew = TimeSpan.FromMinutes(15);

        public string Endpoint => app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();

        public static async Task<SkewedStorage> StartAsync(TimeSpan ahead)
        {
            // An empty builder, so that the settings of the Server in the output folder add no endpoint
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
            builder.WebHost.UseKestrelCore().ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
            var app = builder.Build();
            app.Run(async context =>
            {
                var now = DateTimeOffset.UtcNow + ahead;
                context.Response.Headers.Date = now.ToString("r", CultureInfo.InvariantCulture);
                context.Response.ContentType = "application/xml";
                var signedAt = DateTimeOffset.ParseExact(context.Request.Headers["x-amz-date"].ToString(),
                    "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                if ((now - signedAt).Duration() > LargestSkew)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync(
                        $"<Error><Code>{TooSkewed}</Code><Message>The difference between the request time and the current time is too large.</Message></Error>");
                    return;
                }

                await context.Response.WriteAsync(
                    """<ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"><KeyCount>0</KeyCount><IsTruncated>false</IsTruncated></ListBucketResult>""");
            });
            await app.StartAsync();
            return new SkewedStorage(app);
        }

        public async ValueTask DisposeAsync() => await app.DisposeAsync();
    }
}
