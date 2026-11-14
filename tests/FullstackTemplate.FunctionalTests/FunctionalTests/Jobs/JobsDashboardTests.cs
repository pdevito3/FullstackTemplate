namespace FullstackTemplate.FunctionalTests.FunctionalTests.Jobs;

using System.Net;
using System.Text.RegularExpressions;
using BackWave.Monitor;
using BackWave.Storage;
using FullstackTemplate.Server.Resources.Entrypoints.Jobs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TestUtilities;

public sealed partial class JobsDashboardTests : IDisposable
{
    // No worker group polls a queue with this prefix, so a job in it stays in the state that the test puts it in
    private const string IdleQueuePrefix = "dashboard-test";

    // A redirect drops the bearer token, so the client must not follow the redirect after a form post. The client
    // keeps the antiforgery cookie, as a browser does.
    private readonly HttpClient _client = TestingWebApplicationFactory.Instance.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public void Dispose() => _client.Dispose();

    [Test]
    public async Task a_request_with_no_token_is_forbidden()
    {
        // Act
        using var result = await _client.GetRequestAsync(JobsDashboard.Path);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task a_user_who_is_not_an_operator_is_forbidden()
    {
        // Arrange
        SignIn($"user-{Guid.NewGuid()}");

        // Act
        using var result = await _client.GetRequestAsync(JobsDashboard.Path);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task an_operator_opens_the_dashboard()
    {
        // Arrange
        SignIn(TestingWebApplicationFactory.JobOperatorSubject);

        // Act
        using var result = await _client.GetRequestAsync(JobsDashboard.Path);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }

    [Test]
    public async Task an_operator_requeues_a_dead_lettered_job_with_the_form_of_the_failures_page()
    {
        // Arrange
        var jobId = await DeadLetteredJobAsync();
        SignIn(TestingWebApplicationFactory.JobOperatorSubject);
        var token = await RequeueTokenAsync(jobId);

        // Act
        using var result = await PostRequeueAsync(jobId, token);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        result.Headers.Location?.OriginalString.ShouldBe($"{JobsDashboard.Path}/jobs/{jobId}");
        var job = await Monitor().GetJobAsync(jobId);
        job.ShouldNotBeNull().State.ShouldBe(JobState.Scheduled);
    }

    [Test]
    public async Task a_requeue_with_no_antiforgery_token_is_refused_and_leaves_the_job()
    {
        // Arrange
        var jobId = await DeadLetteredJobAsync();
        SignIn(TestingWebApplicationFactory.JobOperatorSubject);

        // Act
        using var result = await PostRequeueAsync(jobId, token: null);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Monitor().GetJobAsync(jobId)).ShouldNotBeNull().State.ShouldBe(JobState.DeadLettered);
    }

    [Test]
    public async Task a_requeue_by_a_user_who_is_not_an_operator_is_forbidden_and_leaves_the_job()
    {
        // Arrange
        var jobId = await DeadLetteredJobAsync();
        SignIn($"user-{Guid.NewGuid()}");

        // Act
        using var result = await PostRequeueAsync(jobId, token: null);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Monitor().GetJobAsync(jobId)).ShouldNotBeNull().State.ShouldBe(JobState.DeadLettered);
    }

    // A pause holds the jobs of all tenants, so not even an operator can pause a queue from the dashboard
    [Test]
    public async Task an_operator_cannot_pause_a_queue()
    {
        // Arrange
        SignIn(TestingWebApplicationFactory.JobOperatorSubject);

        // Act
        using var result = await _client.PostAsync(
            $"{JobsDashboard.Path}/queues/{IdleQueuePrefix}/pause", new FormUrlEncodedContent([]));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private void SignIn(string subject) => _client.SetFakeBearerToken(new Dictionary<string, object>
    {
        ["sub"] = subject,
        ["azp"] = TestingWebApplicationFactory.FirstPartyClientId,
    });

    /// <summary>
    /// Reads the antiforgery token of the requeue form of the job on the failures page. The response also sets the
    /// antiforgery cookie.
    /// </summary>
    private async Task<string> RequeueTokenAsync(Guid jobId)
    {
        using var page = await _client.GetRequestAsync($"{JobsDashboard.Path}/failures");
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        page.Headers.GetValues("Set-Cookie").ShouldContain(cookie => cookie.StartsWith($"{JobsDashboard.AntiforgeryCookieName}=", StringComparison.Ordinal));
        var html = await page.Content.ReadAsStringAsync();
        html.ShouldContain($"{JobsDashboard.Path}/jobs/{jobId}/requeue");
        var match = AntiforgeryField().Match(html);
        match.Success.ShouldBeTrue("The failures page has no requeue form with an antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups["token"].Value);
    }

    private Task<HttpResponseMessage> PostRequeueAsync(Guid jobId, string? token)
        => _client.PostAsync($"{JobsDashboard.Path}/jobs/{jobId}/requeue", new FormUrlEncodedContent(
            token is null ? [] : [new KeyValuePair<string, string>("__RequestVerificationToken", token)]));

    private static BackWaveMonitor Monitor()
        => TestingWebApplicationFactory.Instance.Services.GetRequiredService<BackWaveMonitor>();

    /// <summary>
    /// A job that ran once and failed with no retry left, as a handler does on its last attempt. Each job has a queue
    /// of its own, so that the claim of a test that runs at the same time cannot take it.
    /// </summary>
    private static async Task<Guid> DeadLetteredJobAsync()
    {
        var store = TestingWebApplicationFactory.Instance.Services.GetRequiredService<IJobStore>();
        var jobId = Guid.NewGuid();
        var queue = $"{IdleQueuePrefix}-{jobId:N}";
        var now = DateTimeOffset.UtcNow;
        (await store.EnqueueAsync(new NewJob(jobId, "dashboard-test-job", "{}"u8.ToArray(), queue, now), now))
            .ShouldBe(EnqueueResult.Ok);
        var claimed = await store.ClaimAsync(new ClaimRequest("dashboard-test", [queue], 1, TimeSpan.FromMinutes(1), now));
        var attempt = claimed.ShouldHaveSingleItem().Attempt;
        (await store.ReportOutcomeAsync(jobId, "dashboard-test", attempt, new JobOutcome.Failure(null, "The test failed the job."), now))
            .ShouldBe(OutcomeResult.Applied);
        return jobId;
    }

    [GeneratedRegex("""name="__RequestVerificationToken" value="(?<token>[^"]+)""")]
    private static partial Regex AntiforgeryField();
}
