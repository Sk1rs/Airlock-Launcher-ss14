using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Character;

namespace SS14.Launcher.ViewModels;

/// <summary>
/// Every character on this machine, with a picture of each drawn by the editor.
/// </summary>
/// <param name="render">Draws a character as the currently selected fork would; null when it cannot.</param>
/// <param name="load">Puts a character into the editor.</param>
public sealed class CharacterGalleryViewModel(
    Func<CharacterProfile, Task<Bitmap?>> render,
    Action<CharacterProfile> load) : ViewModelBase
{
    private readonly LocalizationManager _loc = LocalizationManager.Instance;

    public ObservableCollection<GalleryEntryViewModel> Entries { get; } = new();

    [Reactive] public string StatusText { get; private set; } = "";

    /// <summary>
    /// Reads the folders and starts drawing previews one after another.
    /// </summary>
    public async Task PopulateAsync()
    {
        Entries.Clear();

        var found = await Task.Run(CharacterLibrary.Scan);

        foreach (var entry in found)
        {
            Entries.Add(new GalleryEntryViewModel(entry, _loc, () => load(entry.Profile)));
        }

        StatusText = found.Count == 0
            ? _loc.GetString("gallery-empty", ("folder", CharacterLibrary.OurFolder))
            : _loc.GetString("gallery-count", ("count", found.Count));

        // One at a time keeps the window responsive; each takes a few milliseconds once cached.
        foreach (var entry in Entries)
        {
            try
            {
                entry.Thumbnail = await render(entry.Entry.Profile);
            }
            catch (Exception e)
            {
                Log.Debug(e, "Could not draw {Name} for the gallery", entry.Name);
            }
        }
    }
}

/// <summary>
/// One card in the gallery.
/// </summary>
public sealed class GalleryEntryViewModel(LibraryEntry entry, LocalizationManager loc, Action load) : ViewModelBase
{
    public LibraryEntry Entry { get; } = entry;

    public string Name => Entry.Profile.Name;

    /// <summary>
    /// Species and fork on one line, the two things that decide whether a character fits a server.
    /// </summary>
    public string Details => $"{Entry.Profile.Species} · {Entry.Profile.ForkId ?? "?"}";

    /// <summary>
    /// Which launcher's folder it came from, or "here" when it is already ours.
    /// </summary>
    public string Source => Entry.IsOurs ? loc.GetString("gallery-source-ours") : Entry.Source;

    [Reactive] public Bitmap? Thumbnail { get; set; }

    public void Load() => load();
}
