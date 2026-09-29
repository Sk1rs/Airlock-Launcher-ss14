using System;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;
using Splat;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models;

/// <summary>
/// The picture behind the launcher, and the knobs that keep it from swallowing the interface.
/// </summary>
/// <remarks>
/// The chosen file is copied into the launcher's data folder rather than remembered by path: a
/// wallpaper picked out of Downloads tends to be moved or deleted, and a launcher that loses its
/// background because of that looks broken. Two settings make any picture usable — how strongly it
/// shows through, and how much it is darkened — because photographs are far too busy to read text
/// over at full strength.
/// </remarks>
public sealed class LauncherBackground : ReactiveObject
{
    /// <summary>
    /// Where the copy lives. One file, replaced each time a new picture is chosen.
    /// </summary>
    public static string StoredPath => Path.Combine(LauncherPaths.DirLocalData, "background.img");

    private readonly DataManager _cfg;

    public static LauncherBackground Instance => Locator.Current.GetRequiredService<LauncherBackground>();

    [Reactive] public Bitmap? Image { get; private set; }

    public bool HasImage => Image != null;

    public LauncherBackground(DataManager cfg)
    {
        _cfg = cfg;

        Reload();
    }

    /// <summary>
    /// How strongly the picture shows through, 0 to 100.
    /// </summary>
    public int Strength
    {
        get => _cfg.GetCVar(CVars.BackgroundStrength);
        set
        {
            _cfg.SetCVar(CVars.BackgroundStrength, Math.Clamp(value, 0, 100));
            _cfg.CommitConfig();

            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(Opacity));
        }
    }

    /// <summary>
    /// How much darkness is laid over it, 0 to 100, so text stays readable.
    /// </summary>
    public int Dim
    {
        get => _cfg.GetCVar(CVars.BackgroundDim);
        set
        {
            _cfg.SetCVar(CVars.BackgroundDim, Math.Clamp(value, 0, 100));
            _cfg.CommitConfig();

            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(DimBrush));
        }
    }

    /// <summary>
    /// True to crop the picture to fill the window, false to fit it whole.
    /// </summary>
    public bool Fill
    {
        get => _cfg.GetCVar(CVars.BackgroundFill);
        set
        {
            _cfg.SetCVar(CVars.BackgroundFill, value);
            _cfg.CommitConfig();

            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(Stretch));
        }
    }

    public double Opacity => Strength / 100.0;

    public Stretch Stretch => Fill ? Stretch.UniformToFill : Stretch.Uniform;

    public IBrush DimBrush => new SolidColorBrush(Colors.Black, Dim / 100.0);

    /// <summary>
    /// Takes a picture the player chose and makes it the background.
    /// </summary>
    /// <returns>Null on success, otherwise why it could not be used.</returns>
    public string? Set(string sourceFile)
    {
        try
        {
            // Loaded before it is kept, so a file that is not really an image is refused rather
            // than leaving the launcher with a background it cannot draw.
            using (var probe = new Bitmap(sourceFile))
            {
                if (probe.PixelSize.Width == 0)
                    return "not an image";
            }

            Directory.CreateDirectory(Path.GetDirectoryName(StoredPath)!);

            Image?.Dispose();
            Image = null;

            File.Copy(sourceFile, StoredPath, overwrite: true);

            _cfg.SetCVar(CVars.BackgroundEnabled, true);
            _cfg.CommitConfig();

            Reload();

            return null;
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not use {File} as the background", sourceFile);

            return e.GetBaseException().Message;
        }
    }

    /// <summary>
    /// Drops the picture and goes back to the plain theme colours.
    /// </summary>
    public void Clear()
    {
        _cfg.SetCVar(CVars.BackgroundEnabled, false);
        _cfg.CommitConfig();

        Image?.Dispose();
        Image = null;

        this.RaisePropertyChanged(nameof(HasImage));

        try
        {
            if (File.Exists(StoredPath))
                File.Delete(StoredPath);
        }
        catch (Exception e)
        {
            Log.Debug(e, "Could not delete the stored background");
        }
    }

    /// <summary>
    /// Reads the stored picture, if there is one and it is wanted.
    /// </summary>
    public void Reload()
    {
        try
        {
            Image = _cfg.GetCVar(CVars.BackgroundEnabled) && File.Exists(StoredPath)
                ? new Bitmap(StoredPath)
                : null;
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not read the stored background");
            Image = null;
        }

        this.RaisePropertyChanged(nameof(HasImage));
        this.RaisePropertyChanged(nameof(Opacity));
        this.RaisePropertyChanged(nameof(DimBrush));
        this.RaisePropertyChanged(nameof(Stretch));
    }
}
