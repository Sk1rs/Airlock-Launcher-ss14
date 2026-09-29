using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace SS14.Launcher.Models.Data;

/// <summary>
/// Keeps track of the servers that were actually connected to, most recent first.
/// </summary>
/// <remarks>
/// Favourites are a manual list; this one fills itself, so you can get back to a server you joined
/// off the list without hunting for it again after the game closes.
/// Stored as JSON in a CVar rather than its own table, since it is a short, throwaway list.
/// </remarks>
public sealed class RecentServers
{
    /// <summary>
    /// How many servers to remember. Older entries fall off the end.
    /// </summary>
    public const int MaxEntries = 10;

    private readonly DataManager _cfg;

    private ImmutableArray<RecentServerEntry> _entries = ImmutableArray<RecentServerEntry>.Empty;

    /// <summary>
    /// Most recently connected server first.
    /// </summary>
    public ImmutableArray<RecentServerEntry> Entries => _entries;

    public event Action? Changed;

    public RecentServers(DataManager cfg)
    {
        _cfg = cfg;
    }

    public void Initialize()
    {
        var stored = _cfg.GetCVar(CVars.RecentServers);
        if (string.IsNullOrWhiteSpace(stored))
            return;

        try
        {
            _entries = JsonSerializer.Deserialize<RecentServerEntry[]>(stored)?.ToImmutableArray()
                       ?? ImmutableArray<RecentServerEntry>.Empty;
        }
        catch (JsonException e)
        {
            Log.Warning(e, "Recent server list is corrupt, starting over");
            _entries = ImmutableArray<RecentServerEntry>.Empty;
        }
    }

    /// <summary>
    /// Moves a server to the top of the list, adding it if it wasn't there.
    /// </summary>
    public void Record(string address, string? name)
    {
        var entry = new RecentServerEntry(address, name, DateTimeOffset.UtcNow);

        var updated = new List<RecentServerEntry>(MaxEntries) { entry };
        updated.AddRange(_entries
            .Where(e => !string.Equals(e.Address, address, StringComparison.OrdinalIgnoreCase))
            .Take(MaxEntries - 1));

        // Keep the name we already knew if this connect didn't come with one (direct connect, ss14:// link).
        if (name == null && _entries.FirstOrDefault(e =>
                string.Equals(e.Address, address, StringComparison.OrdinalIgnoreCase)) is { Name: { } knownName })
        {
            updated[0] = entry with { Name = knownName };
        }

        _entries = updated.ToImmutableArray();
        Save();
    }

    public void Clear()
    {
        _entries = ImmutableArray<RecentServerEntry>.Empty;
        Save();
    }

    private void Save()
    {
        _cfg.SetCVar(CVars.RecentServers, JsonSerializer.Serialize(_entries));
        _cfg.CommitConfig();

        Changed?.Invoke();
    }
}

public sealed record RecentServerEntry(
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("lastPlayed")] DateTimeOffset LastPlayed);
