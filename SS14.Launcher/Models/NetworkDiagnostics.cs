using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace SS14.Launcher.Models;

/// <summary>
/// How a host answered.
/// </summary>
public enum HostVerdict
{
    Ok,
    Slow,
    Throttled,
    Unreachable,
}

/// <summary>
/// One host worth checking, and what to fetch from it.
/// </summary>
/// <param name="Group">Which part of the launcher depends on it.</param>
/// <param name="Url">What to fetch.</param>
/// <param name="MeasureSpeed">
///     True to keep reading for a few seconds and judge throughput; false to judge only how long
///     the first response takes.
/// </param>
public sealed record HostCheck(string Group, string Url, bool MeasureSpeed)
{
    public string Host => Uri.TryCreate(Url, UriKind.Absolute, out var uri) ? uri.Host : Url;
}

/// <summary>
/// What came back from one host.
/// </summary>
/// <param name="Latency">Time until the response headers arrived.</param>
/// <param name="BytesPerSecond">Throughput over the sampling window, when it was measured.</param>
/// <param name="Error">Why the host could not be reached, when it could not.</param>
public sealed record HostResult(HostVerdict Verdict, TimeSpan Latency, double? BytesPerSecond, string? Error);

/// <summary>
/// Checks every host the launcher relies on and says which ones are fine, slow or dead from here.
/// </summary>
/// <remarks>
/// Some networks throttle particular hosts to a crawl while leaving the connection open, and from
/// inside the launcher that looks like a hang with no explanation. This does in ten seconds what
/// otherwise takes an evening of guessing: it fetches something small from each host, and for the
/// ones that serve big files it keeps reading for a moment to see whether bytes actually flow.
/// </remarks>
public sealed class NetworkDiagnostics(HttpClient http)
{
    /// <summary>
    /// How long a speed sample runs. Long enough to get past TCP slow start, short enough that a
    /// dozen hosts in parallel finish before the user loses interest.
    /// </summary>
    private static readonly TimeSpan SampleWindow = TimeSpan.FromSeconds(4);

    private static readonly TimeSpan ReachTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Below this a large download is painful; below a tenth of it, it is effectively blocked.
    /// </summary>
    private const double SlowBytesPerSecond = 200 * 1024;

    private static readonly TimeSpan SlowLatency = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The hosts the launcher talks to, in the order they matter to a player.
    /// </summary>
    public static IReadOnlyList<HostCheck> DefaultChecks()
    {
        var checks = new List<HostCheck>();

        foreach (var hub in ConfigConstants.DefaultHubUrls)
        {
            checks.Add(new HostCheck("hubs", new Uri(hub, "api/servers").ToString(), MeasureSpeed: false));
        }

        foreach (var (name, auth) in ConfigConstants.AuthUrls)
        {
            if (name == ConfigConstants.CustomAuthServer)
                continue;

            checks.Add(new HostCheck("auth", auth.AuthPingUrl, MeasureSpeed: false));
        }

        // The engine manifest lives on a CDN; the builds themselves are hosted elsewhere. The
        // manifest is a megabyte, so reading it doubles as a speed sample for both.
        foreach (var manifest in ConfigConstants.EngineBuildsUrl["Robust"].Urls)
        {
            checks.Add(new HostCheck("engine", manifest, MeasureSpeed: true));
        }

        checks.Add(new HostCheck("engine", "https://robust-builds.playss14.com/manifest.json", MeasureSpeed: true));

        // What the character editor pulls from.
        checks.Add(new HostCheck("github", "https://raw.githubusercontent.com/space-wizards/space-station-14/master/README.md", MeasureSpeed: false));
        checks.Add(new HostCheck("github", "https://api.github.com/rate_limit", MeasureSpeed: false));
        checks.Add(new HostCheck("github", "https://cdn.jsdelivr.net/gh/space-wizards/space-station-14@master/README.md", MeasureSpeed: false));

        return checks.DistinctBy(check => check.Url).ToList();
    }

    /// <summary>
    /// Runs one check.
    /// </summary>
    public async Task<HostResult> RunAsync(HostCheck check, CancellationToken cancel = default)
    {
        var watch = Stopwatch.StartNew();

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            timeout.CancelAfter(ReachTimeout + (check.MeasureSpeed ? SampleWindow : TimeSpan.Zero));

            using var response = await http.GetAsync(check.Url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var latency = watch.Elapsed;

            // A 4xx still proves the host is there and answering quickly; only 5xx is a failure.
            if ((int) response.StatusCode >= 500)
                return new HostResult(HostVerdict.Unreachable, latency, null, $"HTTP {(int) response.StatusCode}");

            if (!check.MeasureSpeed)
            {
                return new HostResult(
                    latency > SlowLatency ? HostVerdict.Slow : HostVerdict.Ok,
                    latency,
                    null,
                    null);
            }

            var speed = await SampleSpeedAsync(response, timeout.Token);

            var verdict = speed switch
            {
                >= SlowBytesPerSecond => HostVerdict.Ok,
                >= SlowBytesPerSecond / 10 => HostVerdict.Slow,
                _ => HostVerdict.Throttled,
            };

            return new HostResult(verdict, latency, speed, null);
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            return new HostResult(HostVerdict.Unreachable, watch.Elapsed, null, "timeout");
        }
        catch (Exception e)
        {
            Log.Debug(e, "Host check failed for {Url}", check.Url);

            return new HostResult(HostVerdict.Unreachable, watch.Elapsed, null, e.GetBaseException().Message);
        }
    }

    /// <summary>
    /// Reads the body for the sample window and reports how fast it came.
    /// </summary>
    /// <remarks>
    /// A file that finishes early is judged on the time it actually took, so a small fast file
    /// does not read as "slow" merely for having little to send.
    /// </remarks>
    private static async Task<double> SampleSpeedAsync(HttpResponseMessage response, CancellationToken cancel)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancel);

        var buffer = new byte[64 * 1024];
        var total = 0L;
        var watch = Stopwatch.StartNew();

        using var window = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        window.CancelAfter(SampleWindow);

        try
        {
            while (watch.Elapsed < SampleWindow)
            {
                var read = await stream.ReadAsync(buffer, window.Token);
                if (read == 0)
                    break;

                total += read;
            }
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            // The window closed mid-read; what arrived so far is the sample.
        }

        var seconds = Math.Max(watch.Elapsed.TotalSeconds, 0.05);

        return total / seconds;
    }
}
