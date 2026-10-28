namespace FullstackTemplate.UnitTests;

internal static class TestConstraints
{
    // TUnit runs test classes in parallel in one process. Aspire reads the Kestrel endpoints of a project from
    // appsettings.{environment}.json with the environment from the ASPNETCORE_ENVIRONMENT variable of the process, and no
    // builder option replaces it. So PortTableTests sets that variable, and each class that builds a host, which reads
    // the variable, must not run at the same time.
    public const string HostEnvironment = nameof(HostEnvironment);
}
