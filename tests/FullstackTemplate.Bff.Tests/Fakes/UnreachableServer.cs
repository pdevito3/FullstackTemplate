namespace FullstackTemplate.Bff.Tests.Fakes;

/// <summary>
/// The URL of a server that is not there. A connection to it fails at once, as to a server that is down.
/// <para>
/// No socket can listen on port 0, because a bind to port 0 gets a free port from the OS. Do not use the port of a
/// stopped server: the OS can give that port to a server of a parallel test, which then answers. A bound socket
/// that does not listen also fails: on macOS a connection to it waits for the timeout, and on Linux another bind
/// can take its port.
/// </para>
/// </summary>
public static class UnreachableServer
{
    public const string Url = "http://127.0.0.1:0";
}
