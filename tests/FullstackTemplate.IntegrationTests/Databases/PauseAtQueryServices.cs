namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class PauseAtQueryServices
{
    /// <summary>A provider whose <see cref="AppDbContext"/> has the pause. Send only the request under test through it.</summary>
    /// <remarks>
    /// A held request waits for its rival, and the rival takes a connection from the pool. Thus the held request opens
    /// its connections outside the pool, so held requests cannot use up the connections that the rivals need.
    /// </remarks>
    public static ServiceProvider BuildServices(this PauseAtQuery pause)
        => TestFixture.BuildServiceProvider(s => s
            .ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(pause)),
            TestFixture.UnpooledConnectionString);
}
