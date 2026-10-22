namespace FullstackTemplate.Bff.Tests.Integration;

using System.Globalization;
using Serilog.Events;
using Shouldly;

public class LogInjectionTests
{
    [Test]
    public async Task a_path_with_an_encoded_crlf_cannot_forge_a_log_line()
    {
        var marker = Guid.NewGuid().ToString("N");
        using var browser = BffApp.Instance.CreateBrowser();

        // No X-CSRF header, so the CSRF check writes a Warning with the path
        await browser.GetAsync($"/api/{marker}%0D%0A[12:00:00 ERR] forged");

        var warning = await WaitForAsync(marker, e => e.Level == LogEventLevel.Warning);
        var request = await WaitForAsync(marker, e => e.Properties.ContainsKey("RequestPath"));

        ((ScalarValue)warning.Properties["Path"]).Value.ShouldBe($"/api/{marker}[12:00:00 ERR] forged");
        // Serilog logs the raw target when the server has it, so the path can stay percent-encoded
        ((string)((ScalarValue)request.Properties["RequestPath"]).Value!).ShouldNotContain('\n');
        foreach (var logEvent in BffApp.Logs.WithPropertyContaining(marker))
        {
            var message = logEvent.RenderMessage(CultureInfo.InvariantCulture);
            message.ShouldNotContain('\r', logEvent.MessageTemplate.Text);
            message.ShouldNotContain('\n', logEvent.MessageTemplate.Text);
        }
    }

    // The request log is written after the response, so it can arrive a moment after the client has the response
    private static async Task<LogEvent> WaitForAsync(string marker, Func<LogEvent, bool> match)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            var found = BffApp.Logs.WithPropertyContaining(marker).FirstOrDefault(match);
            if (found is not null)
                return found;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"No matching log event for {marker}.");
            await Task.Delay(20);
        }
    }
}
