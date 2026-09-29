using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace SS14.Launcher.Models.Data;

/// <summary>
/// Tracks how much time was spent playing on each server.
/// </summary>
/// <remarks>
/// Servers publish no per-player statistics, so everything here is measured locally: the launcher
/// already waits for the game client to exit, and that wait is the length of a session.
/// Only whole sessions count, so closing the launcher while the game is running loses that one.
/// </remarks>
public sealed class GameStatistics
{
    /// <summary>
    /// Sessions shorter than this don't count. A client that dies on startup shouldn't show up as
    /// "played", and it exits well within this window.
    /// </summary>
    public static readonly TimeSpan MinimumSession = TimeSpan.FromSeconds(30);

    private readonly DataManager _cfg;

    private ImmutableArray<ServerPlaytime> _entries = ImmutableArray<ServerPlaytime>.Empty;

    /// <summary>
    /// One entry per server ever played on, longest played first.
    /// </summary>
    public ImmutableArray<ServerPlaytime> Entries => _entries;

    public TimeSpan TotalPlaytime => TimeSpan.FromSeconds(_entries.Sum(e => e.TotalSeconds));

    public int TotalSessions => _entries.Sum(e => e.Sessions);

    public event Action? Changed;

    public GameStatistics(DataManager cfg)
    {
        _cfg = cfg;
    }

    public void Initialize()
    {
        var stored = _cfg.GetCVar(CVars.GameStats);
        if (string.IsNullOrWhiteSpace(stored))
            return;

        try
        {
            _entries = Sort(JsonSerializer.Deserialize<ServerPlaytime[]>(stored) ?? []);
        }
        catch (JsonException e)
        {
            Log.Warning(e, "Game statistics are corrupt, starting over");
            _entries = ImmutableArray<ServerPlaytime>.Empty;
        }
    }

    /// <summary>
    /// Adds a finished session to the server's totals.
    /// </summary>
    /// <param name="name">Server name to show, if we happen to know one. Overwrites a previous name.</param>
    public void RecordSession(string address, string? name, TimeSpan duration)
    {
        if (duration < MinimumSession)
        {
            Log.Debug("Ignoring {Duration} session on {Address}, too short to count", duration, address);
            return;
        }

        _entries = Sort(Merge(_entries, address, name, duration));

        _cfg.SetCVar(CVars.GameStats, JsonSerializer.Serialize(_entries));
        _cfg.CommitConfig();

        Log.Information("Recorded {Duration} played on {Address}", duration, address);

        Changed?.Invoke();
    }

    public void Clear()
    {
        _entries = ImmutableArray<ServerPlaytime>.Empty;

        _cfg.SetCVar(CVars.GameStats, "[]");
        _cfg.CommitConfig();

        Changed?.Invoke();
    }

    /// <summary>
    /// Folds a session into an existing list of totals.
    /// </summary>
    public static IEnumerable<ServerPlaytime> Merge(
        IEnumerable<ServerPlaytime> entries,
        string address,
        string? name,
        TimeSpan duration)
    {
        var now = DateTimeOffset.UtcNow;
        var found = false;

        foreach (var entry in entries)
        {
            if (!string.Equals(entry.Address, address, StringComparison.OrdinalIgnoreCase))
            {
                yield return entry;
                continue;
            }

            found = true;

            yield return entry with
            {
                Name = name ?? entry.Name,
                TotalSeconds = entry.TotalSeconds + duration.TotalSeconds,
                Sessions = entry.Sessions + 1,
                LastPlayed = now,
            };
        }

        if (!found)
            yield return new ServerPlaytime(address, name, duration.TotalSeconds, 1, now, now);
    }

    private static ImmutableArray<ServerPlaytime> Sort(IEnumerable<ServerPlaytime> entries)
    {
        return entries.OrderByDescending(e => e.TotalSeconds).ToImmutableArray();
    }
}

/// <summary>
/// Everything we know about time spent on one server.
/// </summary>
public sealed record ServerPlaytime(
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("seconds")] double TotalSeconds,
    [property: JsonPropertyName("sessions")] int Sessions,
    [property: JsonPropertyName("firstPlayed")] DateTimeOffset FirstPlayed,
    [property: JsonPropertyName("lastPlayed")] DateTimeOffset LastPlayed)
{
    public TimeSpan Total => TimeSpan.FromSeconds(TotalSeconds);
}
