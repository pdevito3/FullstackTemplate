namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class PauseAtQueryServices
{
    /// <summary>A provider whose <see cref="AppDbContext"/> has the pause. Send only the request under test through it.</summary>
    public static ServiceProvider BuildServices(this PauseAtQuery pause)
        => TestFixture.BuildServiceProvider(s => s
            .ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(pause)));
}
