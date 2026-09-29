using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Serilog;
using SS14.Launcher.Api;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models;

/// <summary>
/// Why a watched server is worth the player's attention right now.
/// </summary>
public enum RoundEvent
{
    /// <summary>A new round is in the lobby: the moment to join and pick a job.</summary>
    LobbyOpened,

    /// <summary>The player count climbed past the threshold the user set.</summary>
    PlayersReached,
}

public sealed record RoundNotification(string Address, string ServerName, RoundEvent Event, int Players);

/// <summary>
/// Where the watcher keeps its settings; the launcher's config in practice, memory in tests.
/// </summary>
public interface IRoundWatchStore
{
    int PlayerThreshold { get; }

    string WatchedJson { get; set; }
}

/// <summary>
/// The watcher's settings as two config values.
/// </summary>
public sealed class CVarRoundWatchStore(DataManager cfg) : IRoundWatchStore
{
    public int PlayerThreshold => cfg.GetCVar(CVars.WatchPlayerThreshold);

    public string WatchedJson
    {
        get => cfg.GetCVar(CVars.WatchedServers);
        set
        {
            cfg.SetCVar(CVars.WatchedServers, value);
            cfg.CommitConfig();
        }
    }
}

/// <summary>
/// Keeps an eye on the servers the user asked about and says when a round is starting.
/// </summary>
/// <remarks>
/// Waiting for a round on a favourite server means re-checking the list every minute by hand.
/// This polls the watched servers' status endpoints in the background and raises an event on
/// the UI thread when one goes back to the lobby, or fills up past the threshold. The first
/// poll after start only records where things stand, so a launcher opened mid-lobby does not
/// greet the user with a burst of stale news.
/// </remarks>
public sealed class RoundWatcher
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IRoundWatchStore _store;
    private readonly HttpClient _http;

    /// <summary>
    /// What each watched server looked like last time; absent until the first poll answers.
    /// </summary>
    private readonly Dictionary<string, (ServerApi.GameRunLevel? RunLevel, int Players)> _last = new();

    private CancellationTokenSource? _loop;

    public event Action<RoundNotification>? Notified;

    public RoundWatcher(IRoundWatchStore store, HttpClient http)
    {
        _store = store;
        _http = http;
    }

    /// <summary>
    /// Addresses being watched, as stored in the config.
    /// </summary>
    public IReadOnlyList<string> Watched
    {
        get
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(_store.WatchedJson) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }

    public bool IsWatched(string address) =>
        Watched.Contains(address, StringComparer.OrdinalIgnoreCase);

    public void SetWatched(string address, bool watched)
    {
        var list = Watched.Where(a => !a.Equals(address, StringComparison.OrdinalIgnoreCase)).ToList();
        if (watched)
            list.Add(address);

        _store.WatchedJson = JsonSerializer.Serialize(list);

        if (!watched)
            _last.Remove(address);

        // Waking the loop makes a freshly watched server get its baseline right away.
        Restart();
    }

    /// <summary>
    /// Starts polling; safe to call more than once.
    /// </summary>
    public void Start() => Restart();

    private void Restart()
    {
        _loop?.Cancel();
        _loop = new CancellationTokenSource();
        _ = RunAsync(_loop.Token);
    }

    private async Task RunAsync(CancellationToken cancel)
    {
        try
        {
            while (!cancel.IsCancellationRequested)
            {
                var watched = Watched;

                if (watched.Count > 0)
                    await Task.WhenAll(watched.Select(address => PollAsync(address, cancel)));

                await Task.Delay(Interval, cancel);
            }
        }
        catch (OperationCanceledException)
        {
            // Restarted or shutting down.
        }
        catch (Exception e)
        {
            Log.Error(e, "Round watcher stopped");
        }
    }

    private async Task PollAsync(string address, CancellationToken cancel)
    {
        if (!UriHelper.TryParseSs14Uri(address, out var uri))
            return;

        ServerApi.ServerStatus? status;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            timeout.CancelAfter(ConfigConstants.ServerStatusTimeout);

            status = await _http.GetFromJsonAsync<ServerApi.ServerStatus>(
                UriHelper.GetServerStatusAddress(uri),
                timeout.Token);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancel.IsCancellationRequested)
        {
            // A server that is down right now is not news; it will be checked again shortly.
            Log.Debug(e, "Watched server {Address} did not answer", address);
            return;
        }

        if (status == null)
            return;

        Evaluate(address, status);
    }

    /// <summary>
    /// Compares a fresh status with the last one and raises whatever changed. Exposed for tests.
    /// </summary>
    internal List<RoundEvent> Evaluate(string address, ServerApi.ServerStatus status)
    {
        var events = new List<RoundEvent>();
        var threshold = _store.PlayerThreshold;

        if (_last.TryGetValue(address, out var last))
        {
            if (status.RunLevel == ServerApi.GameRunLevel.PreRoundLobby
                && last.RunLevel != ServerApi.GameRunLevel.PreRoundLobby)
            {
                events.Add(RoundEvent.LobbyOpened);
            }

            if (threshold > 0 && status.PlayerCount >= threshold && last.Players < threshold)
                events.Add(RoundEvent.PlayersReached);
        }

        _last[address] = (status.RunLevel, status.PlayerCount);

        foreach (var roundEvent in events)
        {
            var notification = new RoundNotification(address, status.Name ?? address, roundEvent, status.PlayerCount);
            Dispatcher.UIThread.Post(() => Notified?.Invoke(notification));
        }

        return events;
    }
}
