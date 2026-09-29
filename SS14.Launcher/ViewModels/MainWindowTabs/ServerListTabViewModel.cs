using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive.Linq;
using System.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.Logins;
using SS14.Launcher.Models.ServerStatus;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public class ServerListTabViewModel : MainWindowTabViewModel
{
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    private readonly MainWindowViewModel _windowVm;
    private readonly ServerListCache _serverListCache;
    private readonly ServerPingCache _pingCache;

    public ObservableCollection<ServerEntryViewModel> SearchedServers { get; } = new();

    private string? _searchString;

    public override string Name => _loc.GetString("tab-servers-title");

    public string? SearchString
    {
        get => _searchString;
        set => this.RaiseAndSetIfChanged(ref _searchString, value);
    }

    private const int throttleMs = 200;

    public bool SpinnerVisible => _serverListCache.Status < RefreshListStatus.Updated;

    public string ListText
    {
        get
        {
            var status = _serverListCache.Status;
            switch (status)
            {
                case RefreshListStatus.Error:
                    return _loc.GetString("tab-servers-list-status-error-hubs",
                        ("answered", _serverListCache.HubOutcome.Answered),
                        ("asked", _serverListCache.HubOutcome.Asked));
                case RefreshListStatus.PartialError:
                    return _loc.GetString("tab-servers-list-status-partial-hubs",
                        ("answered", _serverListCache.HubOutcome.Answered),
                        ("asked", _serverListCache.HubOutcome.Asked));
                case RefreshListStatus.Cached:
                    return _loc.GetString("tab-servers-list-status-cached");
                case RefreshListStatus.UpdatingMaster:
                    return _loc.GetString("tab-servers-list-status-updating-master");
                case RefreshListStatus.NotUpdated:
                    return "";
                case RefreshListStatus.Updated:
                default:
                    if (SearchedServers.Count == 0 && _serverListCache.AllServers.Count != 0)
                        return _loc.GetString("tab-servers-list-status-none-filtered");

                    if (_serverListCache.AllServers.Count == 0)
                        return _loc.GetString("tab-servers-list-status-none");

                    return "";
            }
        }
    }

    [Reactive] public bool FiltersVisible { get; set; }

    public ServerListFiltersViewModel Filters { get; }

    public ServerListTabViewModel(MainWindowViewModel windowVm)
    {
        Filters = new ServerListFiltersViewModel(windowVm.Cfg, _loc);
        Filters.FiltersUpdated += FiltersOnFiltersUpdated;

        _windowVm = windowVm;
        _serverListCache = Locator.Current.GetRequiredService<ServerListCache>();
        _pingCache = Locator.Current.GetRequiredService<ServerPingCache>();

        SortOptions = ServerSortOption.All
            .Select(sort => new ServerSortOptionViewModel(sort))
            .ToArray();

        _serverListCache.AllServers.CollectionChanged += ServerListUpdated;

        _serverListCache.PropertyChanged += (_, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(ServerListCache.Status):
                    this.RaisePropertyChanged(nameof(ListText));
                    this.RaisePropertyChanged(nameof(SpinnerVisible));
                    break;
            }
        };

        _loc.LanguageSwitched += () => Filters.UpdatePresentFilters(_serverListCache.AllServers);

        this.WhenAnyValue(x => x.SearchString)
            .Throttle(TimeSpan.FromMilliseconds(throttleMs), RxApp.MainThreadScheduler)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateSearchedList());
    }

    private void FiltersOnFiltersUpdated()
    {
        UpdateSearchedList();
    }

    public override void Selected()
    {
        _serverListCache.RequestInitialUpdate();
    }

    public void RefreshPressed()
    {
        _serverListCache.RequestRefresh();
    }

    private void ServerListUpdated(object? sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
    {
        Filters.UpdatePresentFilters(_serverListCache.AllServers);

        UpdateSearchedList();
    }

    /// <summary>
    /// Orderings offered in the sort drop-down.
    /// </summary>
    public ServerSortOptionViewModel[] SortOptions { get; }

    public ServerSortOptionViewModel SelectedSort
    {
        get
        {
            var saved = _windowVm.Cfg.GetCVar(CVars.ServerListSort);

            return SortOptions.FirstOrDefault(o => o.Id == saved) ?? SortOptions[0];
        }
        set
        {
            if (value.Id == _windowVm.Cfg.GetCVar(CVars.ServerListSort))
                return;

            _windowVm.Cfg.SetCVar(CVars.ServerListSort, value.Id);
            _windowVm.Cfg.CommitConfig();

            this.RaisePropertyChanged();
            UpdateSearchedList();
        }
    }

    private void UpdateSearchedList()
    {
        var sortList = new List<ServerStatusData>();

        foreach (var server in _serverListCache.AllServers)
        {
            if (!DoesSearchMatch(server))
                continue;

            sortList.Add(server);
        }

        // Deduplicate servers
        sortList = sortList.Select(s => s.Address).Distinct().Select(a => sortList.First(s => s.Address == a)).ToList();

        Filters.ApplyFilters(sortList);

        sortList.Sort(ServerSortComparer.For(SelectedSort.Id));

        // The list is the only place that knows which servers are actually on screen,
        // so this is where pinging gets kicked off.
        foreach (var server in sortList)
        {
            _pingCache.RequestPing(server);
        }

        SearchedServers.Clear();
        foreach (var server in sortList)
        {
            var vm = new ServerEntryViewModel(_windowVm, server, _serverListCache, _windowVm.Cfg, Locator.Current.GetRequiredService<LoginManager>());
            SearchedServers.Add(vm);
        }

        this.RaisePropertyChanged(nameof(ListText));
    }

    private bool DoesSearchMatch(ServerStatusData data)
    {
        if (string.IsNullOrWhiteSpace(SearchString))
            return true;

        return data.Name != null &&
               data.Name.Contains(SearchString, StringComparison.CurrentCultureIgnoreCase);
    }

    private sealed class ServerSortComparer(string sort) : NotNullComparer<ServerStatusData>
    {
        private static readonly ServerSortComparer Players = new(ServerSortOption.Players);
        private static readonly ServerSortComparer Ping = new(ServerSortOption.Ping);
        private static readonly ServerSortComparer Name = new(ServerSortOption.Name);

        public static ServerSortComparer For(string sort) => sort switch
        {
            ServerSortOption.Ping => Ping,
            ServerSortOption.Name => Name,
            _ => Players,
        };

        public override int Compare(ServerStatusData x, ServerStatusData y)
        {
            if (sort == ServerSortOption.Ping)
            {
                // Servers we have no measurement for go last instead of pretending to be instant.
                var res = (x.Ping ?? TimeSpan.MaxValue).CompareTo(y.Ping ?? TimeSpan.MaxValue);
                if (res != 0)
                    return res;
            }
            else if (sort != ServerSortOption.Name)
            {
                // Sort by player count descending.
                var res = x.PlayerCount.CompareTo(y.PlayerCount);
                if (res != 0)
                    return -res;
            }

            // Sort by name.
            var byName = string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase);
            if (byName != 0)
                return byName;

            // Sort by address.
            return string.Compare(x.Address, y.Address, StringComparison.Ordinal);
        }
    }
}

/// <summary>
/// The orderings the server list can be sorted by.
/// </summary>
public static class ServerSortOption
{
    public const string Players = "players";
    public const string Ping = "ping";
    public const string Name = "name";

    public static readonly string[] All = [Players, Ping, Name];
}

/// <summary>
/// One entry in the sort drop-down.
/// </summary>
public sealed class ServerSortOptionViewModel(string id)
{
    public string Id { get; } = id;

    public string Name => LocalizationManager.Instance.GetString($"tab-servers-sort-{Id}");

    public override string ToString() => Name;
}
