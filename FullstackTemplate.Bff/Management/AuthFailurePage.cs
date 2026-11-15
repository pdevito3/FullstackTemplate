namespace FullstackTemplate.Bff.Management;

/// <summary>
/// The page of the SPA that a navigation to a refused or failed login or logout goes to. Its URL holds no detail of the
/// failure. A request that is not a navigation keeps its problem.
/// </summary>
public sealed class AuthFailurePage
{
    private readonly PathString _path;

    private AuthFailurePage(string path) => _path = path;

    public static AuthFailurePage SignIn { get; } = new("/sign-in-failed");

    public static AuthFailurePage SignOut { get; } = new("/sign-out-failed");

    public static bool IsNavigation(HttpRequest request) => request.Headers["Sec-Fetch-Mode"] == "navigate";

    public void Redirect(HttpContext context)
    {
        var url = LocalUrl.TryParse(context.Request.PathBase.Add(_path).ToUriComponent(), out var page) ? page : LocalUrl.Root;
        context.Response.Redirect(context.RequestServices.GetRequiredService<SpaOrigin>().Resolve(url));
    }
}
