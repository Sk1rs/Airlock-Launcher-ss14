using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels;

/// <summary>
/// Drives the "import from another launcher" dialog.
/// </summary>
public sealed class ImportViewModel : ViewModelBase
{
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    private readonly DataManager _cfg;
    private readonly GameStatistics _stats;

    public ObservableCollection<ImportSourceViewModel> Sources { get; } = new();

    [Reactive] public ImportSourceViewModel? SelectedSource { get; set; }

    [Reactive] public bool ImportFavorites { get; set; } = true;
    [Reactive] public bool ImportHubs { get; set; }
    [Reactive] public bool ImportStats { get; set; } = true;
    [Reactive] public bool ImportContent { get; set; } = true;

    [Reactive] public bool Busy { get; private set; }
    [Reactive] public string StatusText { get; private set; } = "";

    public ImportViewModel()
    {
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _stats = Locator.Current.GetRequiredService<GameStatistics>();
    }

    public bool NothingFound => Sources.Count == 0;

    public void Populate()
    {
        Sources.Clear();

        foreach (var source in LauncherImport.Detect())
        {
            Sources.Add(new ImportSourceViewModel(_loc, source));
        }

        SelectedSource = Sources.Count > 0 ? Sources[0] : null;

        this.RaisePropertyChanged(nameof(NothingFound));
    }

    public async Task RunImport()
    {
        if (SelectedSource is not { } selected || Busy)
            return;

        Busy = true;
        StatusText = _loc.GetString("import-busy");

        // The content database can be several gigabytes, so keep the UI alive while it copies.
        var result = await Task.Run(() => LauncherImport.Import(
            selected.Source,
            ImportFavorites,
            ImportHubs,
            ImportStats && selected.Source.HasSessions,
            ImportContent && selected.Source.HasContent,
            _cfg,
            _stats));

        StatusText = FormatResult(result);
        Busy = false;
    }

    private string FormatResult(LauncherImport.Result result)
    {
        var text = _loc.GetString("import-done",
            ("favorites", result.Favorites),
            ("hubs", result.Hubs));

        if (result.Sessions > 0)
            text += "\n" + _loc.GetString("import-stats-ok", ("sessions", result.Sessions));
        else if (result.StatsError != null)
            text += "\n" + _loc.GetString($"import-stats-error-{result.StatsError}");

        if (result.Content)
            text += "\n" + _loc.GetString("import-content-ok");
        else if (result.ContentError != null)
            text += "\n" + _loc.GetString($"import-content-error-{result.ContentError}");

        return text;
    }
}

/// <summary>
/// One detected launcher install.
/// </summary>
public sealed class ImportSourceViewModel(LocalizationManager loc, LauncherImport.Source source)
{
    public LauncherImport.Source Source { get; } = source;

    public string Title => source.DisplayName;

    public string Description => loc.GetString("import-source-description",
        ("favorites", source.FavoriteCount),
        ("hubs", source.HubCount),
        ("content", source.HasContent
            ? Helpers.FormatBytes(source.ContentBytes)
            : loc.GetString("import-source-no-content")),
        ("sessions", source.SessionCount),
        ("dir", source.DataDirName));
}
