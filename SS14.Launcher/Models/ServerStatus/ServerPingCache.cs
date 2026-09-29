using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace SS14.Launcher.Models.ServerStatus;

/// <summary>
/// Measures how far away a game server is by timing a TCP connection to it.
/// </summary>
/// <remarks>
/// The game's status API listens on the same port as the game itself, so a TCP handshake is a decent
/// stand-in for latency and, unlike ICMP, isn't dropped by half the hosting providers out there.
/// DNS resolution happens before the clock starts so a slow lookup doesn't get counted as latency.
/// </remarks>
public sealed class ServerPingCache
{
    private static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How many servers we're willing to poke at the same time. The server list can be hundreds of
    /// entries long, and opening all those sockets at once upsets routers and antiviruses alike.
    /// </summary>
    private const int MaxConcurrentPings = 16;

    private readonly SemaphoreSlim _limit = new(MaxConcurrentPings, MaxConcurrentPings);

    // Keyed by the status object rather than the address: the list hands out fresh objects on every
    // refresh, which is exactly when we want to measure again. Entries disappear with the objects.
    private readonly ConditionalWeakTable<ServerStatusData, object> _started = new();

    /// <summary>
    /// Measures the server's ping unless it is already measured or being measured right now.
    /// </summary>
    /// <remarks>
    /// Must be called from the UI thread; the result is written back on it.
    /// </remarks>
    public void RequestPing(ServerStatusData data)
    {
        if (!_started.TryAdd(data, this))
            return;

        _ = PingCore(data);
    }

    private async Task PingCore(ServerStatusData data)
    {
        try
        {
            if (!UriHelper.TryParseSs14Uri(data.Address, out var uri))
                return;

            var port = uri.IsDefaultPort
                ? uri.Scheme == UriHelper.SchemeSs14s ? 443 : Global.DefaultServerPort
                : uri.Port;

            using var cancel = new CancellationTokenSource(PingTimeout);

            var addresses = await Dns.GetHostAddressesAsync(uri.Host, cancel.Token);
            if (addresses.Length == 0)
                return;

            await _limit.WaitAsync(cancel.Token);
            try
            {
                var endPoint = new IPEndPoint(addresses[0], port);
                using var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                var stopwatch = Stopwatch.StartNew();
                await socket.ConnectAsync(endPoint, cancel.Token);
                stopwatch.Stop();

                data.Ping = stopwatch.Elapsed;
            }
            finally
            {
                _limit.Release();
            }
        }
        catch (Exception e)
        {
            // Unreachable, refused, timed out: all the same to us, the column just stays empty.
            Log.Verbose(e, "Failed to ping {Address}", data.Address);
        }
    }
}
