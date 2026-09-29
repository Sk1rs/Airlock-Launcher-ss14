using System;
using System.Collections.ObjectModel;
using System.Globalization;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

/// <summary>
/// Shows how much time was spent on which server.
/// </summary>
public sealed class StatsTabViewModel : MainWindowTabViewModel
{
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    private readonly GameStatistics _stats;

    public override string Name => _loc.GetString("tab-stats-title");

    public ObservableCollection<ServerPlaytimeViewModel> Servers { get; } = new();

    /// <summary>
    /// Set while the clear button is waiting for a second click, so stats aren't wiped by accident.
    /// </summary>
    [Reactive] public bool ConfirmingClear { get; private set; }

    public StatsTabViewModel()
    {
        _stats = Locator.Current.GetRequiredService<GameStatistics>();
        _stats.Changed += Update;

        Update();
    }

    public bool IsEmpty => Servers.Count == 0;

    public string SummaryText => _loc.GetString("stats-summary",
        ("time", FormatDuration(_loc, _stats.TotalPlaytime)),
        ("servers", _stats.Entries.Length),
        ("sessions", _stats.TotalSessions));

    public override void Selected()
    {
        Update();
    }

    public void ClearPressed()
    {
        ConfirmingClear = true;
    }

    public void ConfirmClearPressed()
    {
        ConfirmingClear = false;
        _stats.Clear();
    }

    public void CancelClearPressed()
    {
        ConfirmingClear = false;
    }

    private void Update()
    {
        Servers.Clear();

        foreach (var entry in _stats.Entries)
        {
            Servers.Add(new ServerPlaytimeViewModel(_loc, entry));
        }

        this.RaisePropertyChanged(nameof(IsEmpty));
        this.RaisePropertyChanged(nameof(SummaryText));
    }

    /// <summary>
    /// Renders a duration as hours and minutes, dropping the hours when there aren't any.
    /// </summary>
    public static string FormatDuration(LocalizationManager loc, TimeSpan time)
    {
        var hours = (int) time.TotalHours;

        if (hours > 0)
            return loc.GetString("stats-duration-hours", ("hours", hours), ("minutes", time.Minutes));

        return loc.GetString("stats-duration-minutes", ("minutes", time.Minutes));
    }
}

/// <summary>
/// One server's row in the statistics table.
/// </summary>
public sealed class ServerPlaytimeViewModel(LocalizationManager loc, ServerPlaytime entry)
{
    public string Name => string.IsNullOrWhiteSpace(entry.Name) ? entry.Address : entry.Name;

    public string Address => entry.Address;

    public string TotalText => StatsTabViewModel.FormatDuration(loc, entry.Total);

    public string SessionsText => loc.GetString("stats-sessions", ("sessions", entry.Sessions));

    public string LastPlayedText => loc.GetString("stats-last-played",
        ("date", entry.LastPlayed.ToLocalTime().ToString("g", CultureInfo.CurrentUICulture)));
}
