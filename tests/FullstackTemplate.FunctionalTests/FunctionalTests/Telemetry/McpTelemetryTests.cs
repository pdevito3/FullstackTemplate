namespace FullstackTemplate.FunctionalTests.FunctionalTests.Telemetry;

using System.Diagnostics;
using System.Net;
using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using ModelContextProtocol;
using Serilog.Events;
using Shouldly;
using TestUtilities;
using Telemetry = FullstackTemplate.Server.Resources.Telemetry;

/// <summary>
/// A tool call that fails before its feature runs has no feature span. The span of its HTTP request must still name
/// the tool, the caller, and the error, and a mistake of the caller must not write an error log.
/// </summary>
public class McpTelemetryTests : TestBase
{
    [Test]
    public async Task a_tool_call_that_the_access_filter_refuses_has_the_tool_and_the_caller_on_its_span()
    {
        // Arrange
        var agent = await SignInThroughDelegatedAgentAsync();
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("add_tenant", new Dictionary<string, object?> { ["name"] = "x" });

        // Assert
        result.ShouldBeToolError("permission_denied");
        var span = await McpSpanOfAsync(agent);
        span.GetTagItem("gen_ai.tool.name").ShouldBe("add_tenant");
        span.GetTagItem("app.caller.kind").ShouldBe("delegated_agent");
        span.GetTagItem("app.caller.client_id").ShouldBe(agent.ClientId);
        span.GetTagItem("app.caller.tenant_id").ShouldBe(agent.TenantId);
        span.GetTagItem("error.type").ShouldBe("permission_denied");
    }

    [Test]
    public async Task an_unknown_tool_has_the_tool_and_the_caller_on_its_span_and_writes_no_error_log()
    {
        // Arrange
        var agent = await SignInThroughDelegatedAgentAsync();
        await using var client = await CreateMcpClientAsync();
        using var parent = Telemetry.Source.StartActivity("test.parent").ShouldNotBeNull();

        // Act
        var call = () => client.CallToolAsync("no_such_tool", new Dictionary<string, object?>()).AsTask();

        // Assert
        (await call.ShouldThrowAsync<McpProtocolException>()).ErrorCode.ShouldBe(McpErrorCode.InvalidParams);
        parent.Stop();
        var span = await McpSpanOfAsync(agent);
        span.GetTagItem("gen_ai.tool.name").ShouldBe("no_such_tool");
        span.GetTagItem("app.caller.kind").ShouldBe("delegated_agent");
        span.GetTagItem("error.type").ShouldBe(typeof(McpProtocolException).FullName);
        ShouldHaveNoWarningOrErrorLog(parent.TraceId);
    }

    [Test]
    public async Task an_argument_of_the_wrong_type_has_the_tool_and_the_caller_on_its_span_and_writes_no_error_log()
    {
        // Arrange
        var agent = await SignInThroughDelegatedAgentAsync();
        await using var client = await CreateMcpClientAsync();
        using var parent = Telemetry.Source.StartActivity("test.parent").ShouldNotBeNull();

        // Act
        var result = await client.CallToolAsync("list_tenants", new Dictionary<string, object?> { ["pageSize"] = "ten" });

        // Assert
        parent.Stop();
        result.ShouldBeToolError("invalid_argument");
        var span = await McpSpanOfAsync(agent);
        span.GetTagItem("gen_ai.tool.name").ShouldBe("list_tenants");
        span.GetTagItem("app.caller.agent_id").ShouldBe(agent.Id);
        span.GetTagItem("error.type").ShouldBe("invalid_argument");
        ShouldHaveNoWarningOrErrorLog(parent.TraceId);
    }

    [Test]
    public async Task a_tool_call_that_succeeds_has_the_tool_and_the_caller_on_its_span_and_no_error()
    {
        // Arrange
        var agent = await SignInThroughDelegatedAgentAsync();
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("list_tenants", new Dictionary<string, object?>());

        // Assert
        result.IsError.ShouldNotBe(true);
        var span = await McpSpanOfAsync(agent);
        span.GetTagItem("gen_ai.tool.name").ShouldBe("list_tenants");
        span.GetTagItem("app.caller.agent_id").ShouldBe(agent.Id);
        span.GetTagItem("error.type").ShouldBeNull();
    }

    private async Task<Agent> SignInThroughDelegatedAgentAsync()
    {
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = "Admin")
            .Under(tenant)
            .AddAgent(out var agent, a => a.Mode = "delegated");
        await InsertAsync(tenant);
        await InsertAsync(admin);
        await InsertAsync(agent);
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["sub"] = admin.Identifier,
            ["azp"] = agent.ClientId,
        });
        return agent;
    }

    // The server exports the span of the HTTP request when the request ends, which can be after the client has read
    // the response. Each test has its own agent, so the agent finds the one tool call of the test.
    private async Task<Activity> McpSpanOfAsync(Agent agent)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var spans = ExportedSpans()
                .Where(a => a.DisplayName == "POST /mcp/" && a.GetTagItem("app.caller.agent_id") as string == agent.Id)
                .ToList();
            if (spans.Count > 0 || DateTime.UtcNow > deadline)
                return spans.ShouldHaveSingleItem();
            await Task.Delay(50);
        }
    }

    // The test server runs the MCP handler in the context of the test, so the logs of the call are in the trace of the
    // parent. The first check makes sure that the trace has the logs of the MCP server, so the second check means something.
    private static void ShouldHaveNoWarningOrErrorLog(ActivityTraceId traceId)
    {
        var logs = TestingWebApplicationFactory.Instance.CapturedLogs.InTrace(traceId);
        logs.ShouldContain(e => e.Properties.ContainsKey("SourceContext")
            && e.Properties["SourceContext"].ToString().Contains("ModelContextProtocol"));
        logs.Where(e => e.Level >= LogEventLevel.Warning)
            .Select(e => $"{e.Level}: {e.MessageTemplate.Text}")
            .ShouldBeEmpty();
    }
}
