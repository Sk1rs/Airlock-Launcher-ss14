using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.Zapret;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels;

/// <summary>
/// The bypass panel: install it, pick a strategy, turn it on and off.
/// </summary>
public sealed class ZapretViewModel : ViewModelBase
{
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    private readonly DataManager _cfg = Locator.Current.GetRequiredService<DataManager>();
    private readonly ZapretManager _zapret = new(Locator.Current.GetRequiredService<HttpClient>());

    public ObservableCollection<string> Strategies { get; } = new();

    [Reactive] public bool Busy { get; private set; }

    [Reactive] public bool Installed { get; private set; }

    [Reactive] public bool Running { get; private set; }

    [Reactive] public string StatusText { get; private set; } = "";

    public bool NotInstalled => !Installed;

    private string? _selectedStrategy;

    public string? SelectedStrategy
    {
        get => _selectedStrategy;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedStrategy, value);

            if (value != null)
            {
                _cfg.SetCVar(CVars.ZapretStrategy, value);
                _cfg.CommitConfig();
            }
        }
    }

    /// <summary>
    /// Start the chosen strategy whenever the launcher starts.
    /// </summary>
    public bool AutoStart
    {
        get => _cfg.GetCVar(CVars.ZapretAutoStart);
        set
        {
            _cfg.SetCVar(CVars.ZapretAutoStart, value);
            _cfg.CommitConfig();

            this.RaisePropertyChanged();
        }
    }

    public ZapretViewModel()
    {
        Refresh();
    }

    public void Refresh()
    {
        Installed = ZapretManager.IsInstalled;
        Running = ZapretManager.IsRunning;

        this.RaisePropertyChanged(nameof(NotInstalled));

        Strategies.Clear();
        foreach (var strategy in ZapretManager.Strategies())
        {
            Strategies.Add(strategy);
        }

        var stored = _cfg.GetCVar(CVars.ZapretStrategy);
        _selectedStrategy = Strategies.Contains(stored) ? stored : Strategies.FirstOrDefault();
        this.RaisePropertyChanged(nameof(SelectedStrategy));

        StatusText = Installed
            ? _loc.GetString(Running ? "zapret-status-running" : "zapret-status-stopped")
            : _loc.GetString("zapret-status-missing");
    }

    public async Task InstallAsync()
    {
        if (Busy)
            return;

        Busy = true;

        var progress = new Progress<string>(step => StatusText = _loc.GetString($"zapret-step-{step}"));
        var error = await _zapret.InstallAsync(progress);

        Busy = false;
        Refresh();

        if (error != null)
            StatusText = _loc.GetString("zapret-install-failed", ("reason", error));
    }

    /// <summary>
    /// Sets up from an archive the player already has, for when the download cannot get through.
    /// </summary>
    public void InstallFromArchive(string path)
    {
        var error = ZapretManager.InstallFromArchive(path);

        Refresh();

        if (error != null)
            StatusText = _loc.GetString("zapret-install-failed", ("reason", error));
    }

    public void StartPressed()
    {
        if (SelectedStrategy is { } strategy && ZapretManager.Start(strategy))
            StatusText = _loc.GetString("zapret-starting");

        ScheduleRefresh();
    }

    public void StopPressed()
    {
        ZapretManager.Stop();
        ScheduleRefresh();
    }

    /// <summary>
    /// The elevation prompt and the process itself take a moment; look again once it settles.
    /// </summary>
    private void ScheduleRefresh()
    {
        Task.Delay(TimeSpan.FromSeconds(3)).ContinueWith(
            _ => Refresh(),
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// Removes it for players who do not need it. It ships with the launcher, so the first run is
    /// where most people will say no thanks.
    /// </summary>
    public void UninstallPressed()
    {
        var error = ZapretManager.Uninstall();

        Refresh();

        StatusText = error == null
            ? _loc.GetString("zapret-removed")
            : _loc.GetString("zapret-remove-failed", ("reason", error));
    }

    public void OpenFolder() => Helpers.OpenFolder(ZapretManager.InstallDir);

    public void OpenProject() => Helpers.OpenUri(new Uri(ZapretManager.ProjectUrl));
}
