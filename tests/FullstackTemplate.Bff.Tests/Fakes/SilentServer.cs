namespace FullstackTemplate.Bff.Tests.Fakes;

using System.Net;
using System.Net.Sockets;

public sealed class SilentServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly List<Socket> _connections = [];

    public SilentServer()
    {
        _listener.Start();
        _ = AcceptAsync();
    }

    public string Url => $"https://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

    private async Task AcceptAsync()
    {
        try
        {
            while (true)
            {
                var connection = await _listener.AcceptSocketAsync();
                lock (_connections)
                    _connections.Add(connection);
            }
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        _listener.Stop();
        lock (_connections)
        {
            foreach (var connection in _connections)
                connection.Dispose();
        }
    }
}
