using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Splat;
using SS14.Launcher.Utility;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SS14.Launcher.Api;
using SS14.Launcher.Models.Data;
using static SS14.Launcher.Api.HubApi;

namespace SS14.Launcher.Models.ServerStatus;

/// <summary>
///     Caches the Hub's server list.
/// </summary>
public sealed class ServerListCache : ReactiveObject, IServerSource
{
    private readonly HubApi _hubApi;
    private readonly DataManager _dataManager;

    private CancellationTokenSource? _refreshCancel;

    public ObservableCollection<ServerStatusData> AllServers => _allServers;
    private readonly ServerListCollection _allServers = new();

    [Reactive]
    public RefreshListStatus Status { get; private set; } = RefreshListStatus.NotUpdated;

    /// <summary>
    /// How many hubs answered last refresh, and how many were asked. Shown to the player, because
    /// "no servers" and "your hubs are unreachable" look identical otherwise.
    /// </summary>
    [Reactive]
    public (int Answered, int Asked) HubOutcome { get; private set; }

    public ServerListCache()
    {
        _hubApi = Locator.Current.GetRequiredService<HubApi>();
        _dataManager = Locator.Current.GetRequiredService<DataManager>();
    }

    /// <summary>
    /// This function requests the initial update from the server if one hasn't already been requested.
    /// </summary>
    public void RequestInitialUpdate()
    {
        if (Status == RefreshListStatus.NotUpdated)
        {
            RequestRefresh();
        }
    }

    /// <summary>
    /// This function performs a refresh.
    /// </summary>
    /// <summary>
    /// How long any one hub gets to answer. Each hub is timed on its own: a shared budget meant a
    /// single dead hub could run the clock out and take everyone else's answers down with it.
    /// </summary>
    private static readonly TimeSpan HubTimeout = TimeSpan.FromSeconds(10);

    public void RequestRefresh()
    {
        _refreshCancel?.Cancel();
        _refreshCancel = new CancellationTokenSource();
        RefreshServerList(_refreshCancel.Token);
    }

    public async void RefreshServerList(CancellationToken cancel)
    {
        // The list is left standing until there is something to replace it with, so a refresh
        // never blanks the screen and a failed one leaves the last good list in place.
        Status = RefreshListStatus.UpdatingMaster;

        try
        {
            var entries = new HashSet<HubServerListEntry>();
            var requests = new List<(Task<ServerListEntry[]> Request, Uri Hub)>();
            var allSucceeded = true;

            // Queue requests, each on its own clock.
            var timeouts = new List<CancellationTokenSource>();

            foreach (var hub in _dataManager.Hubs.OrderBy(h => h.Priority))
            {
                var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                timeout.CancelAfter(HubTimeout);
                timeouts.Add(timeout);

                requests.Add((_hubApi.GetServers(hub.Address, timeout.Token), hub.Address));
            }

            // Await all requests
            try
            {
                // await Task.Delay(2000, cancel);
                await Task.WhenAll(requests.Select(t => t.Request));
            }
            catch
            {
                // Let's handle any exceptions later, when we have more context
            }

            // Process responses
            foreach (var (request, hub) in requests)
            {
                if (!request.IsCompletedSuccessfully)
                {
                    if (request.IsFaulted)
                    {
                        // request.Exception is non-null, see https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.isfaulted?view=net-7.0#remarks
                        foreach (var ex in request.Exception!.InnerExceptions)
                        {
                            Log.Warning("Request to hub {HubAddress} failed: {Message}", hub, ex.Message);
                        }
                    }
                    else if (request.IsCanceled)
                    {
                        Log.Warning("Request to hub {HubAddress} failed: canceled", hub);
                    }

                    allSucceeded = false;
                    continue;
                }

                foreach (var entry in request.Result)
                {
                    // Don't add server if it was already provided by another hub with higher priority
                    var maybeNewEntry = new HubServerListEntry(entry.Address, hub.AbsoluteUri, entry.StatusData);
                    if (!entries.Add(maybeNewEntry))
                    {
                        Log.Verbose("Not adding {Entry} from {ThisHub} because it was already provided by {PreviousHub}",
                            entry.Address,
                            hub.AbsoluteUri,
                            maybeNewEntry.HubAddress);
                    }
                }
            }

            _allServers.Clear();
            AddEntries(entries);

            HubOutcome = (requests.Count(r => r.Request.IsCompletedSuccessfully), requests.Count);

            Log.Information(
                "Server list: {Count} servers from {Good} of {Total} hubs",
                _allServers.Count,
                HubOutcome.Answered,
                HubOutcome.Asked);

            foreach (var timeout in timeouts)
            {
                timeout.Dispose();
            }

            if (_allServers.Count == 0)
            {
                // Every hub failed us. Fall back on whatever we saw the last time this worked,
                // so a hub outage or a flaky connection doesn't leave you staring at an empty list.
                if (LoadCachedList())
                    Status = RefreshListStatus.Cached;
                else
                    Status = RefreshListStatus.Error;
            }
            else if (!allSucceeded)
                // Some hubs succeeded and returned data
                Status = RefreshListStatus.PartialError;
            else
                Status = RefreshListStatus.Updated;

            if (_allServers.Count != 0)
                SaveCachedList(entries);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to fetch server list due to exception");

            Status = LoadCachedList() ? RefreshListStatus.Cached : RefreshListStatus.Error;
        }
    }

    private void AddEntries(IEnumerable<HubServerListEntry> entries)
    {
        _allServers.AddItems(entries.Select(entry =>
        {
            var statusData = new ServerStatusData(entry.Address, entry.HubAddress);
            ServerStatusCache.ApplyStatus(statusData, entry.StatusData);
            return statusData;
        }));
    }

    /// <summary>
    /// Where the last successfully fetched server list is kept.
    /// </summary>
    private static string CachePath => Path.Combine(LauncherPaths.DirLocalData, "server_list_cache.json");

    private static void SaveCachedList(IEnumerable<HubServerListEntry> entries)
    {
        try
        {
            File.WriteAllText(CachePath, JsonSerializer.Serialize(entries.ToArray()));
        }
        catch (Exception e)
        {
            // Not being able to cache the list is not worth bothering the user about.
            Log.Warning(e, "Failed to write server list cache");
        }
    }

    /// <summary>
    /// Fills the list from the last cached copy.
    /// </summary>
    /// <returns>False if there is no usable cache.</returns>
    private bool LoadCachedList()
    {
        try
        {
            if (!File.Exists(CachePath))
                return false;

            var entries = JsonSerializer.Deserialize<HubServerListEntry[]>(File.ReadAllText(CachePath));
            if (entries == null || entries.Length == 0)
                return false;

            Log.Information("Showing {Count} servers from the cached list", entries.Length);
            AddEntries(entries);

            return true;
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to read server list cache");
            return false;
        }
    }

    void IServerSource.UpdateInfoFor(ServerStatusData statusData)
    {
        if (statusData.HubAddress == null)
        {
            Log.Error("Tried to get server info for hubbed server {Name} without HubAddress set", statusData.Name);
            return;
        }

        ServerStatusCache.UpdateInfoForCore(
            statusData,
            async token => await _hubApi.GetServerInfo(statusData.Address, statusData.HubAddress, token));
    }

    private sealed class ServerListCollection : ObservableCollection<ServerStatusData>
    {
        public void AddItems(IEnumerable<ServerStatusData> items)
        {
            foreach (var item in items)
            {
                Items.Add(item);
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}

public class ServerStatusDataWithFallbackName
{
    public readonly ServerStatusData Data;
    public readonly string? FallbackName;

    public ServerStatusDataWithFallbackName(ServerStatusData data, string? name)
    {
        Data = data;
        FallbackName = name;
    }
}

public enum RefreshListStatus
{
    /// <summary>
    /// Hasn't started updating yet?
    /// </summary>
    NotUpdated,

    /// <summary>
    /// Fetching master server list.
    /// </summary>
    UpdatingMaster,

    /// <summary>
    /// Fetched information from ALL servers from the hub.
    /// </summary>
    Updated,

    /// <summary>
    /// No hub could be reached, so the list is the one saved from a previous run.
    /// </summary>
    Cached,

    /// <summary>
    /// A connection error occured when fetching from at least one hub.
    /// </summary>
    PartialError,

    /// <summary>
    /// An error occured.
    /// </summary>
    Error,
}

public sealed record HubServerListEntry(string Address, string HubAddress, ServerApi.ServerStatus StatusData);
