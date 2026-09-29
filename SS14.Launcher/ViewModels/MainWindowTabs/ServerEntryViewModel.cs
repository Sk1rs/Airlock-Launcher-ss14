using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Messaging;
using Splat;
using SS14.Launcher.Api;
using SS14.Launcher.Models;
using SS14.Launcher.Utility;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.Logins;
using SS14.Launcher.Models.ServerStatus;
using SS14.Launcher.Views;
using static SS14.Launcher.Utility.HubUtility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public sealed class ServerEntryViewModel : ObservableRecipient, IRecipient<FavoritesChanged>, IViewModelBase
{
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    private readonly ServerStatusData _cacheData;
    private readonly IServerSource _serverSource;
    private readonly DataManager _cfg;
    private readonly LoginManager _loginManager;
    private readonly MainWindowViewModel _windowVm;
    private string Address => _cacheData.Address;
    private string _fallbackName = string.Empty;
    private bool _isExpanded;

    public ServerEntryViewModel(MainWindowViewModel windowVm, ServerStatusData cacheData, IServerSource serverSource,
        DataManager cfg, LoginManager loginManager)
    {
        _windowVm = windowVm;
        _cacheData = cacheData;
        _serverSource = serverSource;
        _cfg = cfg;
        _loginManager = loginManager;
    }

    public ServerEntryViewModel(
        MainWindowViewModel windowVm,
        ServerStatusData cacheData,
        FavoriteServer favorite,
        IServerSource serverSource,
        DataManager cfg,
        LoginManager loginManager)
        : this(windowVm, cacheData, serverSource, cfg, loginManager)
    {
        Favorite = favorite;
    }

    public ServerEntryViewModel(
        MainWindowViewModel windowVm,
        ServerStatusDataWithFallbackName ssdfb,
        IServerSource serverSource,
        DataManager cfg,
        LoginManager loginManager)
        : this(windowVm, ssdfb.Data, serverSource, cfg, loginManager)
    {
        FallbackName = ssdfb.FallbackName ?? "";
    }

    public void Tick()
    {
        OnPropertyChanged(nameof(RoundStartTime));
    }

    public void ConnectPressed()
    {
        // If we have an active account that matches the allowed auths, connect
        if (_cacheData.Auths.Contains(_loginManager.ActiveAccount?.ServerUrl))
        {
            ConnectingViewModel.StartConnect(_windowVm, Address);
            return;
        }

        // If there are multiple supported auth methods and we haven't picked a matching one, show a dialog to select an account that fits
        var dialog = new SelectAccountDialog(_cacheData.Auths, _loginManager);
        dialog.Closed += (_, _) =>
        {
            if (dialog.SelectedAccount != null)
                _windowVm.TrySwitchToAccount(dialog.SelectedAccount);
            else
                return;

            ConnectingViewModel.StartConnect(_windowVm, Address);
        };

        dialog.ShowDialog((_windowVm.Control?.GetVisualRoot() as Window)!);
    }

    public FavoriteServer? Favorite { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            CheckUpdateInfo();
        }
    }

    public string Name => Favorite?.Name ?? _cacheData.Name ?? _fallbackName;

    private string FavoriteButtonText => IsFavorite
        ? _loc.GetString("server-entry-remove-favorite")
        : _loc.GetString("server-entry-add-favorite");

    public bool IsFavorite => _cfg.FavoriteServers.Lookup(Address).HasValue;

    private RoundWatcher Watcher => Locator.Current.GetRequiredService<RoundWatcher>();

    /// <summary>
    /// The player's own note about this server, saved as they type it.
    /// </summary>
    public string Note
    {
        get => Locator.Current.GetRequiredService<ServerNotes>().Get(Address);
        set
        {
            Locator.Current.GetRequiredService<ServerNotes>().Set(Address, value);

            OnPropertyChanged(nameof(Note));
            OnPropertyChanged(nameof(HasNote));
        }
    }

    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    /// <summary>
    /// Whether the round watcher is keeping an eye on this server.
    /// </summary>
    public bool IsWatched => Watcher.IsWatched(Address);

    public string WatchButtonText => _loc.GetString(IsWatched ? "server-entry-unwatch" : "server-entry-watch");

    public void WatchButtonPressed()
    {
        Watcher.SetWatched(Address, !IsWatched);

        OnPropertyChanged(nameof(IsWatched));
        OnPropertyChanged(nameof(WatchButtonText));
    }

    public bool ViewedInFavoritesPane { get; set; }

    public bool HaveData => _cacheData.Status == ServerStatusCode.Online;

    public string ServerStatusString
    {
        get
        {
            switch (_cacheData.Status)
            {
                case ServerStatusCode.Offline:
                    return _loc.GetString("server-entry-offline");
                case ServerStatusCode.FetchingStatus:
                case ServerStatusCode.Online:
                    return _loc.GetString("server-entry-fetching");
                default:
                    throw new NotSupportedException();
            }
        }
    }

    // Give a ratio for servers with a defined player count, or just a current number for those without.
    public string PlayerCountString =>
        _loc.GetString("server-entry-player-count",
            ("players", _cacheData.PlayerCount), ("max", _cacheData.SoftMaxPlayerCount));


    public DateTime? RoundStartTime => _cacheData.RoundStartTime;

    public bool HasPing => _cacheData.Ping != null;

    public string PingString => _cacheData.Ping is { } ping
        ? _loc.GetString("server-entry-ping", ("ping", (int) Math.Round(ping.TotalMilliseconds)))
        : "";

    /// <summary>
    /// Green/yellow/red by how bad the latency is. Deliberately fixed colours: they have to mean the
    /// same thing in every palette.
    /// </summary>
    public IBrush PingBrush => _cacheData.Ping is { } ping
        ? ping.TotalMilliseconds switch
        {
            < 100 => PingGoodBrush,
            < 200 => PingOkBrush,
            _ => PingBadBrush,
        }
        : PingOkBrush;

    private static readonly IBrush PingGoodBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0xBF, 0x73));
    private static readonly IBrush PingOkBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0xC4, 0x41));
    private static readonly IBrush PingBadBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x6F, 0x6F));

    /// <summary>
    /// What the round column says when it is not counting: the lobby, or the wrap-up after a round.
    /// Empty while a round runs, which hands the cell to the timer.
    /// </summary>
    public string RoundStatusString => _cacheData.RoundStatus switch
    {
        GameRoundStatus.InLobby => _loc.GetString("server-entry-status-lobby"),
        GameRoundStatus.PostRound => _loc.GetString("server-entry-status-post-round"),
        GameRoundStatus.InRound when _cacheData.RoundStartTime == null => _loc.GetString("server-entry-status-in-round"),
        _ => "",
    };

    public string Description
    {
        get
        {
            switch (_cacheData.Status)
            {
                case ServerStatusCode.Offline:
                    return _loc.GetString("server-entry-description-offline");
                case ServerStatusCode.FetchingStatus:
                    return _loc.GetString("server-entry-description-fetching");
            }

            return _cacheData.StatusInfo switch
            {
                ServerStatusInfoCode.NotFetched => _loc.GetString("server-entry-description-fetching"),
                ServerStatusInfoCode.Fetching => _loc.GetString("server-entry-description-fetching"),
                ServerStatusInfoCode.Error => _loc.GetString("server-entry-description-error"),
                ServerStatusInfoCode.Fetched => _cacheData.Description ??
                                                _loc.GetString("server-entry-description-none"),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public bool IsOnline => _cacheData.Status == ServerStatusCode.Online;

    public string FallbackName
    {
        get => _fallbackName;
        set
        {
            SetProperty(ref _fallbackName, value);
            OnPropertyChanged(nameof(Name));
        }
    }

    public ServerStatusData CacheData => _cacheData;

    /// <summary>
    /// The server's <c>ss14://</c> address, for the copy button.
    /// </summary>
    public string ServerAddress => _cacheData.Address;

    public object ShownTags => _cacheData.Tags.Where(t => t != ServerApi.Tags.TagNoTagInfer)
        .Select(t =>
        {
            var sp = t.Split(':');
            return _loc.GetString($"tag-base-{sp[0]}") + (t.Contains(':') ? $": {_loc.GetString($"tag-{sp[0]}-{sp[1]}")}" : "");
        });

    public object ShownAuths => _cacheData.Auths;
    public bool ShowAuths => _cacheData.Auths.Length > 0;

    public string? FetchedFrom
    {
        get
        {
            if (_cfg.HasCustomHubs)
            {
                return _cacheData.HubAddress == null
                    ? null
                    : _loc.GetString("server-fetched-from-hub", ("hub", GetHubShortName(_cacheData.HubAddress)));
            }

            return null;
        }
    }

    public bool ShowFetchedFrom => _cfg.HasCustomHubs && !ViewedInFavoritesPane;

    public void FavoriteButtonPressed()
    {
        if (IsFavorite)
        {
            // Remove favorite.
            _cfg.RemoveFavoriteServer(_cfg.FavoriteServers.Lookup(Address).Value);
        }
        else
        {
            var fav = new FavoriteServer(_cacheData.Name ?? FallbackName, Address);
            _cfg.AddFavoriteServer(fav);
        }

        _cfg.CommitConfig();
    }

    public void FavoriteRaiseButtonPressed()
    {
        if (IsFavorite)
            _cfg.ReorderFavoriteServer(_cfg.FavoriteServers.Lookup(Address).Value, 1);

        _cfg.CommitConfig();
    }

    public void FavoriteLowerButtonPressed()
    {
        if (IsFavorite)
            _cfg.ReorderFavoriteServer(_cfg.FavoriteServers.Lookup(Address).Value, -1);

        _cfg.CommitConfig();
    }

    public void Receive(FavoritesChanged message)
    {
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(FavoriteButtonText));
    }

    private void CheckUpdateInfo()
    {
        if (!IsExpanded || _cacheData.Status != ServerStatusCode.Online)
            return;

        if (_cacheData.StatusInfo is not (ServerStatusInfoCode.NotFetched or ServerStatusInfoCode.Error))
            return;

        _serverSource.UpdateInfoFor(_cacheData);
    }

    protected override void OnActivated()
    {
        base.OnActivated();

        _cacheData.PropertyChanged += OnCacheDataOnPropertyChanged;
    }

    protected override void OnDeactivated()
    {
        base.OnDeactivated();

        _cacheData.PropertyChanged -= OnCacheDataOnPropertyChanged;
    }

    private void OnCacheDataOnPropertyChanged(object? _, PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(IServerStatusData.PlayerCount):
            case nameof(IServerStatusData.SoftMaxPlayerCount):
                OnPropertyChanged(nameof(ServerStatusString));
                OnPropertyChanged(nameof(PlayerCountString));
                break;

            case nameof(IServerStatusData.RoundStartTime):
                OnPropertyChanged(nameof(RoundStartTime));
                break;

            case nameof(ServerStatusData.Ping):
                OnPropertyChanged(nameof(HasPing));
                OnPropertyChanged(nameof(PingString));
                OnPropertyChanged(nameof(PingBrush));
                break;

            case nameof(IServerStatusData.RoundStatus):
                OnPropertyChanged(nameof(RoundStatusString));
                break;

            case nameof(IServerStatusData.Status):
                OnPropertyChanged(nameof(IsOnline));
                OnPropertyChanged(nameof(ServerStatusString));
                OnPropertyChanged(nameof(PlayerCountString));
                OnPropertyChanged(nameof(Description));
                OnPropertyChanged(nameof(HaveData));
                CheckUpdateInfo();
                break;

            case nameof(IServerStatusData.Name):
                OnPropertyChanged(nameof(Name));
                break;

            case nameof(IServerStatusData.Description):
            case nameof(IServerStatusData.StatusInfo):
                OnPropertyChanged(nameof(Description));
                OnPropertyChanged(nameof(HaveData));
                break;
        }
    }

    public async void UpdateFavoriteInfo()
    {
        if (Favorite == null
            || _windowVm.Control?.GetVisualRoot() is not Window window)
            return;

        var (name, address) = await new AddFavoriteDialog(Favorite.Name ?? "", Favorite.Address).ShowDialog<(string name, string address)>(window);

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(address))
            return;

        try
        {
            _cfg.EditFavoriteServer(new(Name, Address), address, name);
            _cfg.CommitConfig();
        }
        catch (ArgumentException)
        {
            // Ignored
        }
    }
}
