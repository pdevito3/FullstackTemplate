namespace FullstackTemplate.SharedTestHelpers.Utilities;

using System.Net;
using System.Net.Sockets;

public static class DockerUtilities
{
    public static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
