namespace FullstackTemplate.UnitTests.Resources;

using FullstackTemplate.Server.Resources.Extensions;
using Microsoft.AspNetCore.Http;
using Serilog.Events;
using Shouldly;

public class RequestLogLevelTests
{
    [Theory]
    [InlineData("/health", 200, LogEventLevel.Verbose)]
    [InlineData("/alive", 200, LogEventLevel.Verbose)]
    [InlineData("/health", 503, LogEventLevel.Error)]
    [InlineData("/api/v1/tenants", 200, LogEventLevel.Information)]
    [InlineData("/api/v1/tenants", 404, LogEventLevel.Information)]
    [InlineData("/api/v1/tenants", 500, LogEventLevel.Error)]
    public void level_depends_on_the_path_and_the_status(string path, int statusCode, LogEventLevel expected)
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Response.StatusCode = statusCode;

        // Act
        var level = HostExtensions.RequestLogLevel(httpContext, 1, null);

        // Assert
        level.ShouldBe(expected);
    }

    [Fact]
    public void an_exception_is_an_error_even_for_a_health_probe()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/health";

        // Act
        var level = HostExtensions.RequestLogLevel(httpContext, 1, new InvalidOperationException("boom"));

        // Assert
        level.ShouldBe(LogEventLevel.Error);
    }
}
