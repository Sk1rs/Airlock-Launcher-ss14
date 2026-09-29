using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;
using SS14.Launcher.Localization;
using SS14.Launcher.Models;
using SS14.Launcher.ViewModels.MainWindowTabs;

namespace SS14.Launcher.ViewModels;

/// <summary>
/// The screenshots the game saved, newest first, without leaving the launcher.
/// </summary>
/// <remarks>
/// Thumbnails are decoded to a fixed width rather than loaded whole: a folder of 4K screenshots
/// is hundreds of megabytes, and nobody wants the launcher to hold that in memory to show a grid.
/// </remarks>
public sealed class ScreenshotGalleryViewModel : ViewModelBase
{
    /// <summary>
    /// Width thumbnails are decoded at. Enough for the grid, cheap enough for a hundred of them.
    /// </summary>
    private const int ThumbnailWidth = 320;

    private readonly LocalizationManager _loc = LocalizationManager.Instance;

    public ObservableCollection<ScreenshotViewModel> Screenshots { get; } = new();

    [Reactive] public string StatusText { get; private set; } = "";

    [Reactive] public bool Empty { get; private set; } = true;

    public async Task PopulateAsync()
    {
        Screenshots.Clear();

        var folder = GuidesTabViewModel.ScreenshotsDirectory;

        // Screenshots taken through other SS14 launchers count too: they are the same game.
        var files = await Task.Run(() => LauncherDataFolders.All(GuideLinks.ScreenshotsFolder)
            .SelectMany(Shots)
            .OrderByDescending(file => file.LastWriteTime)
            .ToList());

        foreach (var file in files)
        {
            Screenshots.Add(new ScreenshotViewModel(file.FullName, file.LastWriteTime, file.Length, Remove));
        }

        Empty = Screenshots.Count == 0;
        StatusText = Empty
            ? _loc.GetString("screenshots-empty", ("folder", folder))
            : _loc.GetString("screenshots-count", ("count", Screenshots.Count));

        foreach (var screenshot in Screenshots)
        {
            await screenshot.LoadThumbnailAsync(ThumbnailWidth);
        }
    }

    /// <summary>
    /// The image files in one folder; a folder that cannot be read contributes nothing.
    /// </summary>
    private static IEnumerable<FileInfo> Shots(string folder)
    {
        try
        {
            return new DirectoryInfo(folder)
                .EnumerateFiles("*.*")
                .Where(file => file.Extension is ".png" or ".jpg" or ".jpeg")
                .ToList();
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not list screenshots in {Folder}", folder);

            return [];
        }
    }

    /// <summary>
    /// Opens the folder itself, for anything this window does not do.
    /// </summary>
    public void OpenFolder() => GuidesTabViewModel.OpenScreenshotsFolder();

    private void Remove(ScreenshotViewModel screenshot)
    {
        Screenshots.Remove(screenshot);

        Empty = Screenshots.Count == 0;
        StatusText = _loc.GetString("screenshots-deleted", ("name", screenshot.Name));
    }
}

/// <summary>
/// One screenshot in the grid.
/// </summary>
public sealed class ScreenshotViewModel(string path, DateTime taken, long bytes, Action<ScreenshotViewModel> removed)
    : ViewModelBase
{
    public string Path { get; } = path;

    public string Name { get; } = System.IO.Path.GetFileName(path);

    /// <summary>
    /// When it was taken and how big it is, which is what you want when picking one to share.
    /// </summary>
    public string Details { get; } = $"{taken:dd.MM.yyyy HH:mm} · {bytes / 1024f / 1024f:0.0} MB";

    [Reactive] public Bitmap? Thumbnail { get; private set; }

    public async Task LoadThumbnailAsync(int width)
    {
        try
        {
            Thumbnail = await Task.Run(() =>
            {
                using var stream = File.OpenRead(Path);

                return Bitmap.DecodeToWidth(stream, width);
            });
        }
        catch (Exception e)
        {
            Log.Debug(e, "Could not read screenshot {Path}", Path);
        }
    }

    /// <summary>
    /// Hands the file to whatever opens images on this machine.
    /// </summary>
    public void Open()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = Path, UseShellExecute = true });
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not open screenshot {Path}", Path);
        }
    }

    /// <summary>
    /// Moves the file to the recycle bin's rough equivalent: a plain delete, but only on request.
    /// </summary>
    public void Delete()
    {
        try
        {
            File.Delete(Path);
            removed(this);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not delete screenshot {Path}", Path);
        }
    }
}
