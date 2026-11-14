namespace FullstackTemplate.IntegrationTests.Examples.Workshops;

using Amazon.Runtime;
using Amazon.S3;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Attachments;
using FullstackTemplate.Server.Domain.ObjectDeletes;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Examples.Workshops.Features;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Attachments;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Shouldly;
using FullstackTemplate.IntegrationTests.ObjectDeletes;
using static FullstackTemplate.IntegrationTests.ObjectDeletes.ObjectDeleteTestServer;

public class WorkshopAttachmentAddTests : TestBase
{
    [Test]
    public async Task a_put_whose_reply_is_lost_after_storage_kept_the_bytes_leaves_its_object_to_the_sweep()
    {
        // Arrange
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out _).AddWorkshop(out var workshop)
            .BuildAsync();
        await using var bucket = await SweepBucket.CreateAsync();
        var server = bucket.Server(configure: services =>
        {
            services.RemoveAll<IAmazonS3>();
            services.AddSingleton<IAmazonS3>(new AmazonS3Client(new BasicAWSCredentials("test", "test"), new AmazonS3Config
            {
                ServiceURL = TestFixture.Storage.ServiceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
                MaxErrorRetry = 0,
                HttpClientFactory = new LostPutReplyClientFactory(),
            }));
        });
        var adder = new TestingServiceScope(server);
        adder.SetUserWithDefaults(identifier: admin.Identifier);
        await using var file = new ReceivedFile(
            new MemoryStream(SampleFiles.OfLength(SampleFiles.Pdf, 100)), "notes.pdf", "application/pdf");
        await Should.ThrowAsync<HttpRequestException>(() => adder.SendAsync(new AddWorkshopAttachment.Command(workshop.Id, file)));
        var kept = (await bucket.ObjectsAsync()).ShouldHaveSingleItem("The add failed, and it left the object that storage kept.");

        // Act
        await bucket.SweepAsync(kept.LastModified + SweepBucket.Grace + TimeSpan.FromSeconds(1));

        // Assert
        (await bucket.KeysAsync()).ShouldBeEmpty("No row names the object, so the sweep deleted it after its grace.");
    }

    [Test]
    public async Task a_canceled_add_on_a_hung_storage_ends_on_the_cancel()
    {
        // Arrange
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out _).AddWorkshop(out var workshop)
            .BuildAsync();
        await using var hung = WithStorageHung(new ObjectDeleteOptions());
        var underTest = new TestingServiceScope(hung.Server);
        underTest.SetUserWithDefaults(identifier: admin.Identifier);
        await using var file = new ReceivedFile(
            new MemoryStream(SampleFiles.OfLength(SampleFiles.Pdf, 100)), "notes.pdf", "application/pdf");
        using var request = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act
        var act = () => underTest.GetService<ISender>()
            .Send(new AddWorkshopAttachment.Command(workshop.Id, file), request.Token)
            .WaitAsync(TimeSpan.FromSeconds(30));

        // Assert
        await act.ShouldThrowAsync<OperationCanceledException>("The put stopped on the cancel.");
    }

    [Test]
    public async Task an_add_on_a_hung_storage_throws_a_timeout_at_its_time_limit_with_no_cancel_of_the_caller()
    {
        // Arrange
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out _).AddWorkshop(out var workshop)
            .BuildAsync();
        await using var hung = WithStorageHung(new ObjectDeleteOptions { AttemptTimeout = TimeSpan.FromSeconds(1) },
            new AttachmentOptions { AddTimeout = TimeSpan.FromSeconds(1) });
        var underTest = new TestingServiceScope(hung.Server);
        underTest.SetUserWithDefaults(identifier: admin.Identifier);
        await using var file = new ReceivedFile(
            new MemoryStream(SampleFiles.OfLength(SampleFiles.Pdf, 100)), "notes.pdf", "application/pdf");
        using var testLimit = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        var act = () => underTest.SendAsync(new AddWorkshopAttachment.Command(workshop.Id, file)).WaitAsync(testLimit.Token);

        // Assert
        await act.ShouldThrowAsync<TimeoutException>("The time limit of the add stopped the put that storage never answered.");
    }

    [Test]
    public async Task an_add_whose_save_waits_on_a_tenant_delete_throws_a_timeout_at_its_time_limit()
    {
        // Arrange
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out _).AddWorkshop(out var workshop)
            .BuildAsync();
        await using var server = TestFixture.BuildServiceProvider(services => services.Replace(
            ServiceDescriptor.Singleton(Options.Create(new AttachmentOptions { AddTimeout = TimeSpan.FromSeconds(2) }))));
        var underTest = new TestingServiceScope(server);
        underTest.SetUserWithDefaults(identifier: admin.Identifier);
        await using var file = new ReceivedFile(
            new MemoryStream(SampleFiles.OfLength(SampleFiles.Pdf, 100)), "notes.pdf", "application/pdf");
        var held = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var tenantDelete = new TestingServiceScope().ExecuteDbContextAsync(dbContext => RootLock.Tenant.RunExclusiveAsync(
            dbContext, tenant.Id, async _ =>
            {
                held.SetResult();
                await release.Task;
                return 0;
            }, CancellationToken.None));
        await held.Task;
        using var testLimit = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act
        Exception? thrown = null;
        try
        {
            await underTest.SendAsync(new AddWorkshopAttachment.Command(workshop.Id, file)).WaitAsync(testLimit.Token);
        }
        catch (Exception exception)
        {
            thrown = exception;
        }
        finally
        {
            release.SetResult();
            await tenantDelete;
        }

        // Assert
        thrown.ShouldBeOfType<TimeoutException>("The time limit of the add stopped the save that waited on the lock.");
    }

    private sealed class LostPutReplyClientFactory : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig)
            => new(new LostPutReplyHandler { InnerHandler = new HttpClientHandler() });
    }

    private sealed class LostPutReplyHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.Method != HttpMethod.Put)
                return response;

            response.Dispose();
            throw new HttpRequestException("The reply of the put was lost.");
        }
    }
}
