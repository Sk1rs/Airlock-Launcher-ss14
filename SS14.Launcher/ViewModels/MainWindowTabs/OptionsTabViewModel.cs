using System;
using SS14.Launcher.Models;
using ReactiveUI.Fody.Helpers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.ContentManagement;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.EngineManager;
using SS14.Launcher.Models.Logins;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public class OptionsTabViewModel : MainWindowTabViewModel
{
    public DataManager Cfg { get; }
    private readonly IEngineManager _engineManager;
    private readonly ContentManager _contentManager;
    private readonly LoginManager _loginMgr;

    public LanguageSelectorViewModel Language { get; } = new();

    private readonly ThemeManager _themes;

    /// <summary>
    /// Colour palettes offered in the drop-down.
    /// </summary>
    public ThemeOptionViewModel[] Themes { get; }

    public ThemeOptionViewModel SelectedTheme
    {
        get => Themes.Single(t => t.Id == _themes.CurrentTheme);
        set
        {
            if (value.Id == _themes.CurrentTheme)
                return;

            _themes.SetTheme(value.Id);
            this.RaisePropertyChanged();
        }
    }

    public OptionsTabViewModel()
    {
        Cfg = Locator.Current.GetRequiredService<DataManager>();
        _loginMgr = Locator.Current.GetRequiredService<LoginManager>();
        _engineManager = Locator.Current.GetRequiredService<IEngineManager>();
        _contentManager = Locator.Current.GetRequiredService<ContentManager>();
        _themes = Locator.Current.GetRequiredService<ThemeManager>();

        Themes = ThemeManager.Themes
            .Select(id => new ThemeOptionViewModel(id))
            .ToArray();

        DisableIncompatibleMacOS = OperatingSystem.IsMacOS();
        _uiScalingX = Cfg.GetCVar(CVars.UiScalingX);
        _uiScalingY = Cfg.GetCVar(CVars.UiScalingY);
    }
    public bool DisableIncompatibleMacOS { get; }

    public override string Name => LocalizationManager.Instance.GetString("tab-options-title");

    public bool CompatMode
    {
        get => Cfg.GetCVar(CVars.CompatMode);
        set
        {
            Cfg.SetCVar(CVars.CompatMode, value);
            Cfg.CommitConfig();
        }
    }

    public bool LogLauncherVerbose
    {
        get => Cfg.GetCVar(CVars.LogLauncherVerbose);
        set
        {
            Cfg.SetCVar(CVars.LogLauncherVerbose, value);
            Cfg.CommitConfig();
        }
    }

    private double _uiScalingX;
    public double UiScalingX
    {
        get => _uiScalingX;
        set => _uiScalingX = Math.Clamp(value, 0.1, 10);
    }

    private double _uiScalingY;
    public double UiScalingY
    {
        get => _uiScalingY;
        set => _uiScalingY = Math.Clamp(value, 0.1, 10);
    }

    /// <summary>
    /// Player count at which a watched server is worth a notification; 0 means never.
    /// </summary>
    public int WatchPlayerThreshold
    {
        get => Cfg.GetCVar(CVars.WatchPlayerThreshold);
        set
        {
            Cfg.SetCVar(CVars.WatchPlayerThreshold, Math.Max(0, value));
            Cfg.CommitConfig();
        }
    }

    /// <summary>
    /// Whether the launcher connects to the last played server by itself on startup.
    /// </summary>
    public bool AutoConnectLast
    {
        get => Cfg.GetCVar(CVars.AutoConnectLast);
        set
        {
            Cfg.SetCVar(CVars.AutoConnectLast, value);
            Cfg.CommitConfig();
        }
    }

    [Reactive] public string StorageText { get; private set; } = "";

    /// <summary>
    /// Measures the folders that grow on their own, so "why is my disk full" has an answer.
    /// </summary>
    public async Task RefreshStorage()
    {
        StorageText = LocalizationManager.Instance.GetString("tab-options-storage-busy");
        StorageText = await Task.Run(DiagnosticsReport.StorageSummary);
    }

    /// <summary>
    /// The picture behind the launcher, so the options can drive it.
    /// </summary>
    public LauncherBackground Background { get; } = LauncherBackground.Instance;

    public bool NotUiScalingLock => !UiScalingLock;
    public bool UiScalingLock
    {
        get => Cfg.GetCVar(CVars.UiScalingLock);
        set
        {
            Cfg.SetCVar(CVars.UiScalingLock, value);
            Cfg.CommitConfig();
        }
    }

    public void ClearEngines()
    {
        _engineManager.ClearAllEngines();
    }

    public async Task<bool> ClearServerContent()
    {
        return await _contentManager.ClearAll();
    }

    public void OpenLogDirectory()
    {
        OpenDirectory(LauncherPaths.DirLogs);
    }

    /// <summary>
    /// Opens the folder holding this launcher's own data: config, engines, logs.
    /// </summary>
    public void OpenDataDirectory()
    {
        OpenDirectory(LauncherPaths.DirUserData);
    }

    /// <summary>
    /// Opens the folder the game client exports character PNGs into.
    /// </summary>
    public void OpenExportsDirectory()
    {
        OpenDirectory(LauncherPaths.DirClientExports);
    }

    private static void OpenDirectory(string path)
    {
        // The client only creates its folders once it has run, so make sure there is something to open.
        Directory.CreateDirectory(path);

        Process.Start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = path
        });
    }

    public void OpenAccountSettings()
    {
        if (_loginMgr.ActiveAccount is not { } account
            || LoginManager.TryGetAccountUrl(account.Server, account.ServerUrl) is not { } url)
            return;
        Helpers.OpenUri(LoginManager.GetAuthServerById(account.Server, account.ServerUrl, url).AccountManUrl);
    }
}

/// <summary>
/// One entry in the theme drop-down.
/// </summary>
public sealed class ThemeOptionViewModel(string id)
{
    public string Id { get; } = id;

    public string Name => LocalizationManager.Instance.GetString($"theme-name-{Id.ToLowerInvariant()}");

    public override string ToString() => Name;
}
