// Run once for each server process:
//   NO_COLOR=1 dotnet csharprepl connect <pid> --eval-file .agents/skills/csharprepl/prelude.csx
// The usings and helpers stay in the server process until it restarts.
using System.Security.Claims;
using System.Threading;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FullstackTemplate.Server.Databases;

// Sends a MediatR request in a new DI scope, as a request with no user.
// Without a user, AppDbContext.CurrentTenantId is null and the tenant filter is off.
async Task<T> Send<T>(IRequest<T> request)
{
    using var scope = Get<IServiceScopeFactory>().CreateScope();
    return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
}

// Sends a MediatR request as the user with this "sub" claim. The tenant filter uses this user's tenant.
async Task<T> SendAs<T>(string sub, IRequest<T> request)
{
    var accessor = Get<IHttpContextAccessor>();
    accessor.HttpContext = new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", sub)], "csharprepl"))
    };
    try
    {
        return await Send(request);
    }
    finally
    {
        accessor.HttpContext = null;
    }
}

// Runs code against a scoped service, for example AppDbContext.
async Task<TResult> InScope<TService, TResult>(Func<TService, Task<TResult>> action) where TService : notnull
{
    using var scope = Get<IServiceScopeFactory>().CreateScope();
    return await action(scope.ServiceProvider.GetRequiredService<TService>());
}

"prelude loaded"
