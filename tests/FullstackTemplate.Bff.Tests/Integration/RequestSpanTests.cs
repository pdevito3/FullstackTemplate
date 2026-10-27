namespace FullstackTemplate.Bff.Tests.Integration;

using System.Collections.Concurrent;
using System.Diagnostics;
using Shouldly;

public sealed class RequestSpanTests : IDisposable
{
    private readonly ConcurrentQueue<Activity> _requestSpans = new();
    private readonly ActivityListener _listener;

    public RequestSpanTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _requestSpans.Enqueue,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Test]
    public async Task request_span_keeps_the_query_keys_and_hides_the_values()
    {
        var key = $"probe_{Guid.NewGuid():N}";
        using var browser = BffApp.Instance.CreateBrowser();

        await browser.GetAsync($"/api/v1/users?filters=email%20%40%3D*%20%22jane%22&{key}=jane");

        var request = await WaitForSpanAsync(key);
        request.GetTagItem("url.query").ShouldBe($"?filters=Redacted&{key}=Redacted");
    }

    // The server ends the request span after it sends the response, so the span can stop a moment after the client
    // has the response
    private async Task<Activity> WaitForSpanAsync(string key)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            var found = _requestSpans.Where(a => a.GetTagItem("url.query") is string query && query.Contains(key, StringComparison.Ordinal)).ToList();
            if (found.Count > 0)
                return found.ShouldHaveSingleItem();
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"No request span for {key}.");
            await Task.Delay(20);
        }
    }
}
