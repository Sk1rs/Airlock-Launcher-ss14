using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.VisualTree;
using DynamicData;
using DynamicData.Alias;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.Logins;
using SS14.Launcher.Models.ServerStatus;
using SS14.Launcher.Utility;
using SS14.Launcher.Views;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public class HomePageViewModel : MainWindowTabViewModel
{
    public MainWindowViewModel MainWindowViewModel { get; }
    private readonly DataManager _cfg;
    private readonly ServerStatusCache _statusCache = new ServerStatusCache();
    private readonly ServerListCache _serverListCache;
    private readonly ClassicServerListCache _classicServerListCache;
    private readonly ServerPingCache _pingCache;
    private readonly RecentServers _recentServers;
    private readonly LoginManager _loginManager;

    /// <summary>
    /// How many recently played servers the home tab shows. More than that and it eats the favourites.
    /// </summary>
    private const int ShownRecents = 5;

    public HomePageViewModel(MainWindowViewModel mainWindowViewModel)
    {
        MainWindowViewModel = mainWindowViewModel;
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _serverListCache = Locator.Current.GetRequiredService<ServerListCache>();
        _classicServerListCache = Locator.Current.GetRequiredService<ClassicServerListCache>();
        _pingCache = Locator.Current.GetRequiredService<ServerPingCache>();
        _recentServers = Locator.Current.GetRequiredService<RecentServers>();
        _loginManager = Locator.Current.GetRequiredService<LoginManager>();

        _recentServers.Changed += UpdateRecents;

        _cfg.FavoriteServers
            .Connect()
            .Select(x =>
            {
                if (x.Address.StartsWith("byond://"))
                {
                    return (IViewModelBase) new ClassicServerEntryViewModel(MainWindowViewModel, _classicServerListCache.GetStatusFor(x.Address), x, _cfg)
                        { ViewedInFavoritesPane = true, IsExpanded = _cfg.ExpandedServers.Contains(x.Address) };
                }

                return (IViewModelBase) new ServerEntryViewModel(MainWindowViewModel, _statusCache.GetStatusFor(x.Address), x, _statusCache, _cfg, Locator.Current.GetRequiredService<LoginManager>())
                    { ViewedInFavoritesPane = true, IsExpanded = _cfg.ExpandedServers.Contains(x.Address) };
            })
            .OnItemAdded(a =>
            {
                #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                if (IsSelected)
                {
                    if (a is ServerEntryViewModel svm)
                        _statusCache.InitialUpdateStatus(svm.CacheData);
                }
                #pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            })
            .Sort(Comparer<IViewModelBase>.Create((a, b) =>
            {
                var afav = (a as ServerEntryViewModel)?.Favorite ?? (a as ClassicServerEntryViewModel)?.Favorite;
                var bfav = (b as ServerEntryViewModel)?.Favorite ?? (b as ClassicServerEntryViewModel)?.Favorite;
                var aName = (a as ServerEntryViewModel)?.Name ?? (a as ClassicServerEntryViewModel)?.Name;
                var bName = (b as ServerEntryViewModel)?.Name ?? (b as ClassicServerEntryViewModel)?.Name;

                var dc = afav!.Position.CompareTo(bfav!.Position);
                return dc != 0 ? -dc : string.Compare(aName, bName, StringComparison.CurrentCultureIgnoreCase);
            }))
            .Bind(out var favorites)
            .Subscribe(_ =>
            {
                FavoritesEmpty = favorites.Count == 0;
            });

        Favorites = favorites;

        // A server that just got favourited should leave the recents list, and vice versa.
        _cfg.FavoriteServers.Connect().Subscribe(_ => UpdateRecents());

        UpdateRecents();
    }

    /// <summary>
    /// Servers connected to recently, most recent first, minus the ones already in favourites.
    /// </summary>
    public ObservableCollection<ServerEntryViewModel> Recents { get; } = new();

    [Reactive] public bool HasRecents { get; private set; }

    public string LastServerName
    {
        get
        {
            if (_recentServers.Entries.IsEmpty)
                return "";

            var last = _recentServers.Entries[0];

            return last.Name ?? last.Address;
        }
    }

    /// <summary>
    /// Connects straight back to the last server played on, no hunting through the list.
    /// </summary>
    public void ReconnectPressed()
    {
        if (_recentServers.Entries.IsEmpty)
            return;

        ConnectingViewModel.StartConnect(MainWindowViewModel, _recentServers.Entries[0].Address);
    }

    public void ClearRecentsPressed()
    {
        _recentServers.Clear();
    }

    private void UpdateRecents()
    {
        Recents.Clear();

        foreach (var entry in _recentServers.Entries)
        {
            if (Recents.Count >= ShownRecents)
                break;

            // Already pinned to the top of the page, no point listing it twice.
            if (_cfg.FavoriteServers.Lookup(entry.Address).HasValue)
                continue;

            var data = _statusCache.GetStatusFor(entry.Address);

            Recents.Add(new ServerEntryViewModel(
                MainWindowViewModel,
                new ServerStatusDataWithFallbackName(data, entry.Name),
                _statusCache,
                _cfg,
                _loginManager));

            _ = _statusCache.InitialUpdateStatus(data);
            _pingCache.RequestPing(data);
        }

        HasRecents = Recents.Count > 0;
        this.RaisePropertyChanged(nameof(LastServerName));
    }

    public ReadOnlyObservableCollection<IViewModelBase> Favorites { get; }
    public ObservableCollection<ServerEntryViewModel> Suggestions { get; } = new();

    [Reactive] public bool FavoritesEmpty { get; private set; } = true;

    public override string Name => LocalizationManager.Instance.GetString("tab-home-title");
    public Control? Control { get; set; }

    public async void DirectConnectPressed()
    {
        if (!TryGetWindow(out var window))
        {
            return;
        }

        var res = await new DirectConnectDialog().ShowDialog<string?>(window);
        if (res == null)
        {
            return;
        }

        ConnectingViewModel.StartConnect(MainWindowViewModel, res);
    }

    public async void AddFavoritePressed()
    {
        if (!TryGetWindow(out var window))
        {
            return;
        }

        var (name, address) = await new AddFavoriteDialog().ShowDialog<(string name, string address)>(window);

        try
        {
            _cfg.AddFavoriteServer(new FavoriteServer(name, address));
            _cfg.CommitConfig();
        }
        catch (ArgumentException)
        {
            // Happens if address already a favorite, so ignore.
            // TODO: Give a popup to the user?
        }
    }

    private bool TryGetWindow([NotNullWhen(true)] out Window? window)
    {
        window = Control?.GetVisualRoot() as Window;
        return window != null;
    }

    public void OpenScreenshotsPressed() => GuidesTabViewModel.OpenScreenshotsFolder();

    public void RefreshPressed()
    {
        _statusCache.Refresh();
        _serverListCache.RequestRefresh();
        _classicServerListCache.Refresh();
    }

    public override void Selected()
    {
        foreach (var favorite in Favorites)
        {
            if (favorite is ServerEntryViewModel svm)
            {
                _ = _statusCache.InitialUpdateStatus(svm.CacheData);
                _pingCache.RequestPing(svm.CacheData);
            }
        }
        _serverListCache.RequestInitialUpdate();
    }
}
