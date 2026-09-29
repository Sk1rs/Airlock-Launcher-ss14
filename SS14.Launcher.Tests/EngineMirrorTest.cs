using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SS14.Launcher.Models.EngineManager;

namespace SS14.Launcher.Tests;

[TestFixture]
[TestOf(typeof(EngineManagerDynamic))]
public sealed class EngineMirrorTest
{
    [Test]
    public void TestMirrorsFollowTheManifestHosts()
    {
        var mirrors = EngineManagerDynamic.EngineMirrors(
            "https://robust-builds.playss14.com/builds/270.1.0/Robust.Client_win-x64.zip",
            "Robust",
            "270.1.0");

        Assert.That(mirrors[0], Is.EqualTo("https://robust-builds.playss14.com/builds/270.1.0/Robust.Client_win-x64.zip"),
            "the host the manifest names goes first");
        Assert.That(mirrors, Does.Contain("https://robust-builds.cdn.spacestation14.com/builds/270.1.0/Robust.Client_win-x64.zip"),
            "the CDN that serves the manifest keeps the builds too");
        Assert.That(mirrors, Is.Unique);
    }

    [Test]
    public void TestUnknownEngineHasNoMirrors()
    {
        var mirrors = EngineManagerDynamic.EngineMirrors("https://example.com/builds/1/x.zip", "NoSuchEngine", "1");

        Assert.That(mirrors, Is.EqualTo(new[] { "https://example.com/builds/1/x.zip" }));
    }

    /// <summary>
    /// A host that sends one byte every few seconds must be abandoned; one that sends the file must not.
    /// </summary>
    [Test]
    public async Task TestStallGuardDropsATricklingHost()
    {
        using var server = new TrickleServer();
        using var http = new HttpClient();

        // Trickling: a byte every 2s never satisfies a 3s stall window (progress reports every 80 KB).
        await using (var sink = new MemoryStream())
        {
            var ok = await EngineManagerDynamic.TryDownloadWithStallGuard(
                http, server.Url("trickle"), sink, null, CancellationToken.None, TimeSpan.FromSeconds(3));

            Assert.That(ok, Is.False, "a crawling host must be given up on");
        }

        // Healthy: 200 KB at once comes through whole.
        await using (var sink = new MemoryStream())
        {
            var ok = await EngineManagerDynamic.TryDownloadWithStallGuard(
                http, server.Url("fast"), sink, null, CancellationToken.None, TimeSpan.FromSeconds(3));

            Assert.That(ok, Is.True);
            Assert.That(sink.Length, Is.EqualTo(TrickleServer.FastSize));
        }
    }

    [Test]
    public async Task TestUserCancelIsNotMistakenForAStall()
    {
        using var server = new TrickleServer();
        using var http = new HttpClient();
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await using var sink = new MemoryStream();

        // The stall window is far longer than the cancel, so the cancel is what fires.
        Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await EngineManagerDynamic.TryDownloadWithStallGuard(
                http, server.Url("trickle"), sink, null, cancel.Token, TimeSpan.FromSeconds(30)),
            "a cancel by the user must surface, not be swallowed as a mirror failure");
    }

    /// <summary>
    /// Tiny local HTTP server: /fast returns a blob at once, /trickle drips one byte every 2 s.
    /// </summary>
    private sealed class TrickleServer : IDisposable
    {
        public const int FastSize = 200 * 1024;

        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _stop = new();
        private readonly int _port;

        public TrickleServer()
        {
            _port = Random.Shared.Next(20000, 40000);
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();
            _ = Task.Run(ServeAsync);
        }

        public string Url(string path) => $"http://127.0.0.1:{_port}/{path}";

        private async Task ServeAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync();
                }
                catch (Exception)
                {
                    return;
                }

                _ = Task.Run(() => HandleAsync(ctx));
            }
        }

        private async Task HandleAsync(HttpListenerContext ctx)
        {
            try
            {
                var trickle = ctx.Request.Url!.AbsolutePath.EndsWith("trickle");
                ctx.Response.ContentLength64 = trickle ? 1024 : FastSize;

                if (!trickle)
                {
                    await ctx.Response.OutputStream.WriteAsync(new byte[FastSize]);
                    ctx.Response.Close();
                    return;
                }

                for (var i = 0; i < 1024 && !_stop.IsCancellationRequested; i++)
                {
                    await ctx.Response.OutputStream.WriteAsync(Encoding.ASCII.GetBytes("x"), _stop.Token);
                    await ctx.Response.OutputStream.FlushAsync(_stop.Token);
                    await Task.Delay(2000, _stop.Token);
                }
            }
            catch (Exception)
            {
                // The client hung up, which is the point.
            }
            finally
            {
                try
                {
                    ctx.Response.Abort();
                }
                catch (Exception)
                {
                    // Already gone.
                }
            }
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
            _listener.Close();
        }
    }
}
