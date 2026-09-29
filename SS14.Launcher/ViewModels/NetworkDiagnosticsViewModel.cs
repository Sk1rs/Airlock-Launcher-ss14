using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels;

/// <summary>
/// Runs the host checks and shows each row as it comes in.
/// </summary>
public sealed class NetworkDiagnosticsViewModel : ViewModelBase
{
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    private readonly NetworkDiagnostics _diagnostics;

    public ObservableCollection<HostRowViewModel> Rows { get; } = new();

    [Reactive] public bool Busy { get; private set; }

    [Reactive] public string Summary { get; private set; } = "";

    public NetworkDiagnosticsViewModel()
    {
        _diagnostics = new NetworkDiagnostics(Locator.Current.GetRequiredService<HttpClient>());

        foreach (var check in NetworkDiagnostics.DefaultChecks())
        {
            Rows.Add(new HostRowViewModel(check, _loc));
        }
    }

    /// <summary>
    /// Checks every host at once; the rows fill in as answers arrive.
    /// </summary>
    public async Task RunAsync()
    {
        if (Busy)
            return;

        Busy = true;
        Summary = _loc.GetString("netdiag-running");

        foreach (var row in Rows)
        {
            row.Reset();
        }

        await Task.WhenAll(Rows.Select(async row =>
        {
            var result = await _diagnostics.RunAsync(row.Check);
            row.Apply(result);
        }));

        var bad = Rows.Count(row => row.Verdict is HostVerdict.Throttled or HostVerdict.Unreachable);
        var slow = Rows.Count(row => row.Verdict == HostVerdict.Slow);

        Summary = bad == 0 && slow == 0
            ? _loc.GetString("netdiag-all-good")
            : _loc.GetString("netdiag-summary", ("bad", bad), ("slow", slow));

        Busy = false;
    }
}

/// <summary>
/// One host in the table.
/// </summary>
public sealed class HostRowViewModel(HostCheck check, LocalizationManager loc) : ViewModelBase
{
    public HostCheck Check { get; } = check;

    public string Group { get; } = loc.GetString($"netdiag-group-{check.Group}");

    public string Host => Check.Host;

    [Reactive] public HostVerdict? Verdict { get; private set; }

    [Reactive] public string Status { get; private set; } = "";

    [Reactive] public string Detail { get; private set; } = "";

    public void Reset()
    {
        Verdict = null;
        Status = loc.GetString("netdiag-pending");
        Detail = "";
    }

    public void Apply(HostResult result)
    {
        Verdict = result.Verdict;

        Status = loc.GetString(result.Verdict switch
        {
            HostVerdict.Ok => "netdiag-ok",
            HostVerdict.Slow => "netdiag-slow",
            HostVerdict.Throttled => "netdiag-throttled",
            _ => "netdiag-unreachable",
        });

        Detail = result switch
        {
            { Error: { } error } => error,
            { BytesPerSecond: { } speed } => $"{FormatSpeed(speed)}, {result.Latency.TotalMilliseconds:0} ms",
            _ => $"{result.Latency.TotalMilliseconds:0} ms",
        };
    }

    private static string FormatSpeed(double bytesPerSecond)
    {
        return bytesPerSecond switch
        {
            >= 1024 * 1024 => $"{bytesPerSecond / (1024 * 1024):0.0} MB/s",
            >= 1024 => $"{bytesPerSecond / 1024:0} KB/s",
            _ => $"{bytesPerSecond:0} B/s",
        };
    }
}
