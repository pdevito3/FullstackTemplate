// Run once for each server process:
//   NO_COLOR=1 dotnet csharprepl connect <pid> --eval-file .agents/skills/csharprepl/prelude.csx
// The usings and helpers stay in the server process until it restarts.
using System.Security.Claims;
using System.Threading;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Services;

// Sends a MediatR request in a new system scope. A system scope sees and manages all tenants.
async Task<T> Send<T>(IRequest<T> request)
{
    await using var scope = Get<IServiceScopeFactory>().CreateSystemScope();
    return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
}

// Sends a MediatR request as the user with this "sub" claim. The tenant filter uses this user's tenant.
// A "sub" with no user record stops here, because the server shows no tenant data to this user.
// It uses a plain scope, not a system scope: a system scope sees all tenants, whatever the user.
async Task<T> SendAs<T>(string sub, IRequest<T> request)
{
    using (var lookupScope = Get<IServiceScopeFactory>().CreateScope())
    {
        if (await lookupScope.ServiceProvider.GetRequiredService<ITenantIdProvider>().FindLiveUserAsync(sub) is null)
            throw new InvalidOperationException($"No user has the identifier '{sub}'. Use the Identifier of a user in the tenant.");
    }

    var accessor = Get<IHttpContextAccessor>();
    accessor.HttpContext = new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", sub), new Claim("client_id", Get<IConfiguration>()["Auth:ClientId"]!)], "csharprepl"))
    };
    try
    {
        using var scope = Get<IServiceScopeFactory>().CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }
    finally
    {
        accessor.HttpContext = null;
    }
}

// Runs code against a scoped service, for example AppDbContext, in a new system scope.
async Task<TResult> InScope<TService, TResult>(Func<TService, Task<TResult>> action) where TService : notnull
{
    await using var scope = Get<IServiceScopeFactory>().CreateSystemScope();
    return await action(scope.ServiceProvider.GetRequiredService<TService>());
}

"prelude loaded"
