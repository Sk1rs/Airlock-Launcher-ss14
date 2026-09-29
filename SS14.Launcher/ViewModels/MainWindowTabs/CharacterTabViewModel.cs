using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Character;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

/// <summary>
/// Builds a character out of a fork's own sprites, without joining a server.
/// </summary>
public sealed class CharacterTabViewModel : MainWindowTabViewModel
{
    /// <summary>
    /// The preview is drawn at the sprite's own size, so blow it up to something you can look at.
    /// </summary>
    private const int PreviewScale = 8;

    /// <summary>
    /// Exported files are bigger again, so they are usable outside the launcher.
    /// </summary>
    private const int ExportScale = 16;

    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    private readonly HttpClient _http;
    private readonly DataManager _cfg;
    private readonly Dictionary<string, CharacterCatalog> _catalogs = new();

    /// <summary>
    /// What the pointer is hovering over in an open drop-down, drawn but not committed.
    /// </summary>
    private (CharacterLayerViewModel Layer, MarkingPrototype? Marking)? _preview;

    private CancellationTokenSource? _renderCancel;
    private CancellationTokenSource? _loadCancel;
    private RenderedCharacter? _lastRender;
    private bool _suppressRender;

    public override string Name => _loc.GetString("tab-character-title");

    public ForkOptionViewModel[] Forks { get; }

    public ObservableCollection<SpeciesOptionViewModel> Species { get; } = new();

    public ObservableCollection<CharacterLayerViewModel> Layers { get; } = new();

    /// <summary>
    /// Jobs to dress the character in; the first entry is no uniform at all.
    /// </summary>
    public ObservableCollection<JobOptionViewModel> Jobs { get; } = new();

    /// <summary>
    /// False when the fork's file list was unavailable, which is what outfits are built from.
    /// </summary>
    [Reactive] public bool JobsAvailable { get; private set; }

    public DirectionOptionViewModel[] Directions { get; }

    [Reactive] public Bitmap? Preview { get; private set; }
    [Reactive] public bool Busy { get; private set; }
    [Reactive] public string StatusText { get; private set; } = "";

    public CharacterTabViewModel()
    {
        _http = Locator.Current.GetRequiredService<HttpClient>();
        _cfg = Locator.Current.GetRequiredService<DataManager>();

        Forks = CharacterFork.All.Select(fork => new ForkOptionViewModel(fork)).ToArray();
        _selectedFork = Forks[0];

        Directions =
        [
            new DirectionOptionViewModel(RsiDirection.South, _loc),
            new DirectionOptionViewModel(RsiDirection.North, _loc),
            new DirectionOptionViewModel(RsiDirection.East, _loc),
            new DirectionOptionViewModel(RsiDirection.West, _loc),
        ];
        _selectedDirection = Directions[0];
    }

    private ForkOptionViewModel _selectedFork;

    public ForkOptionViewModel SelectedFork
    {
        get => _selectedFork;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedFork, value);
            _ = LoadForkAsync();
        }
    }

    private SpeciesOptionViewModel? _selectedSpecies;

    public SpeciesOptionViewModel? SelectedSpecies
    {
        get => _selectedSpecies;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedSpecies, value);
            this.RaisePropertyChanged(nameof(UsesSkinTone));
            RebuildLayers();
            QueueRender();
        }
    }

    private JobOptionViewModel? _selectedJob;

    public JobOptionViewModel? SelectedJob
    {
        get => _selectedJob;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedJob, value);
            QueueRender();
        }
    }

    private DirectionOptionViewModel _selectedDirection;

    public DirectionOptionViewModel SelectedDirection
    {
        get => _selectedDirection;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedDirection, value);
            QueueRender();
        }
    }

    private bool _female;

    public bool Female
    {
        get => _female;
        set
        {
            this.RaiseAndSetIfChanged(ref _female, value);
            QueueRender();
        }
    }

    private int _skinTone = 20;

    /// <summary>
    /// Position on the game's human skin tone scale.
    /// </summary>
    public int SkinTone
    {
        get => _skinTone;
        set
        {
            this.RaiseAndSetIfChanged(ref _skinTone, value);
            this.RaisePropertyChanged(nameof(SkinColorHex));
            QueueRender();
        }
    }

    private string _skinColorHex = CharacterColor.FromSkinTone(20).ToHex();

    /// <summary>
    /// Skin colour for species the game lets you colour freely.
    /// </summary>
    public string SkinColorHex
    {
        get => UsesSkinTone ? CharacterColor.FromSkinTone(SkinTone).ToHex() : _skinColorHex;
        set
        {
            this.RaiseAndSetIfChanged(ref _skinColorHex, value);
            QueueRender();
        }
    }

    private string _characterName = "";

    /// <summary>
    /// Name written into an exported character file.
    /// </summary>
    public string CharacterName
    {
        get => _characterName;
        set => this.RaiseAndSetIfChanged(ref _characterName, value);
    }

    private int _age = 21;

    public int Age
    {
        get => _age;
        set => this.RaiseAndSetIfChanged(ref _age, value);
    }

    private string _eyeColorHex = "#7A503C";

    public string EyeColorHex
    {
        get => _eyeColorHex;
        set
        {
            this.RaiseAndSetIfChanged(ref _eyeColorHex, value);
            QueueRender();
        }
    }

    /// <summary>
    /// Whether this species uses the tone slider rather than a free colour.
    /// </summary>
    public bool UsesSkinTone => SelectedSpecies?.Entry.UsesSkinTone ?? true;

    public bool NotUsesSkinTone => !UsesSkinTone;

    /// <summary>
    /// Fetch many files at once. Takes effect the next time a fork is loaded.
    /// </summary>
    public bool FastDownload
    {
        get => _cfg.GetCVar(CVars.CharacterFastDownload);
        set
        {
            _cfg.SetCVar(CVars.CharacterFastDownload, value);
            _cfg.CommitConfig();

            this.RaisePropertyChanged();
        }
    }

    public override void Selected()
    {
        if (Species.Count == 0 && !Busy)
            _ = LoadForkAsync();
    }

    /// <summary>
    /// Downloads (or reads from cache) the selected fork's species and markings.
    /// </summary>
    private async Task LoadForkAsync()
    {
        _loadCancel?.Cancel();
        _loadCancel = new CancellationTokenSource();
        var cancel = _loadCancel.Token;

        Busy = true;
        StatusText = _loc.GetString("character-loading");

        try
        {
            var fork = SelectedFork.Fork;

            if (!_catalogs.TryGetValue(fork.Id, out var catalog))
            {
                catalog = new CharacterCatalog(new ForkResources(fork, _http))
                {
                    FastDownload = FastDownload,
                    Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                };
                await Task.Run(() => catalog.LoadAsync(cancel: cancel), cancel);
                _catalogs[fork.Id] = catalog;
            }

            if (cancel.IsCancellationRequested)
                return;

            _suppressRender = true;

            Species.Clear();
            foreach (var species in catalog.Species)
            {
                Species.Add(new SpeciesOptionViewModel(species, catalog.Locale.Get(species.NameKey) ?? species.Name));
            }

            Jobs.Clear();
            Jobs.Add(new JobOptionViewModel(null, _loc.GetString("character-job-none")));
            foreach (var job in catalog.Jobs.Jobs)
            {
                Jobs.Add(new JobOptionViewModel(job, catalog.Locale.Get(job.NameKey) ?? job.DisplayName));
            }

            SelectedJob = Jobs[0];
            JobsAvailable = Jobs.Count > 1;

            SelectedSpecies = Species.FirstOrDefault(s => s.Entry.Name == "Human") ?? Species.FirstOrDefault();

            _suppressRender = false;

            StatusText = _loc.GetString("character-loaded",
                ("species", Species.Count),
                ("markings", catalog.Markings.Values.Sum(list => list.Count)));

            // Without the repository listing we only found the paths every fork shares, so say so
            // rather than letting the fork's own additions look like they don't exist.
            if (catalog.LimitedDiscovery)
                StatusText += " " + _loc.GetString("character-limited");

            QueueRender();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load character resources");
            StatusText = _loc.GetString("character-load-failed");
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>
    /// Rebuilds the marking drop-downs for the selected species.
    /// </summary>
    private void RebuildLayers()
    {
        Layers.Clear();

        if (SelectedSpecies is not { } species || !_catalogs.TryGetValue(SelectedFork.Fork.Id, out var catalog))
            return;

        // Only offer layers this species actually has something for, in drawing order.
        foreach (var bodyPart in CharacterRenderer.LayerOrder)
        {
            var options = catalog.MarkingsFor(bodyPart, species.Entry.Name);
            if (options.Count == 0)
                continue;

            Layers.Add(new CharacterLayerViewModel(bodyPart, options, _loc, catalog.Locale, QueueRender));
        }
    }

    /// <summary>
    /// Shows a marking on the character without selecting it, for as long as the pointer rests on it.
    /// </summary>
    public void PreviewMarking(MarkingOptionViewModel option)
    {
        if (option.Layer is not { } layer)
            return;

        // Nothing to show if it is already the selection.
        if (_preview?.Layer == layer && _preview?.Marking == option.Marking)
            return;

        _preview = (layer, option.Marking);
        QueueRender();
    }

    /// <summary>
    /// Drops the hover preview and goes back to what is actually selected.
    /// </summary>
    public void ClearPreview()
    {
        if (_preview == null)
            return;

        _preview = null;
        QueueRender();
    }

    public void QueueRender()
    {
        if (_suppressRender)
            return;

        _renderCancel?.Cancel();
        _renderCancel = new CancellationTokenSource();

        _ = RenderAsync(_renderCancel.Token);
    }

    private async Task RenderAsync(CancellationToken cancel)
    {
        if (SelectedSpecies is not { } species || !_catalogs.TryGetValue(SelectedFork.Fork.Id, out var catalog))
            return;

        var config = new CharacterConfig
        {
            Species = species.Entry,
            Female = Female,
            Direction = SelectedDirection.Direction,
            SkinColor = UsesSkinTone
                ? CharacterColor.FromSkinTone(SkinTone)
                : CharacterColor.FromHex(SkinColorHex),
            EyeColor = CharacterColor.FromHex(EyeColorHex),
        };

        var preview = _preview;

        foreach (var layer in Layers)
        {
            // The hovered option stands in for whatever this layer has selected.
            var marking = preview?.Layer == layer ? preview.Value.Marking : layer.SelectedMarking?.Marking;

            if (marking != null)
                config.Markings[layer.BodyPart] = new MarkingSelection(marking, CharacterColor.FromHex(layer.ColorHex));
        }

        if (SelectedJob?.Job is { } job)
        {
            foreach (var (slot, item) in catalog.Jobs.OutfitFor(job))
            {
                config.Outfit[slot] = item;
            }
        }

        try
        {
            var renderer = new CharacterRenderer(catalog);
            var rendered = await Task.Run(() => renderer.RenderAsync(config, cancel), cancel);

            if (cancel.IsCancellationRequested)
                return;

            _lastRender = rendered;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Preview = rendered?.Scale(PreviewScale).ToBitmap();

                if (rendered == null)
                    StatusText = _loc.GetString("character-render-empty");
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to render character");
            StatusText = _loc.GetString("character-render-failed");
        }
    }

    /// <summary>
    /// Writes the character out as a PNG.
    /// </summary>
    public void Export(string path)
    {
        if (_lastRender is not { } rendered)
            return;

        try
        {
            rendered.Scale(ExportScale).SavePng(path);
            StatusText = _loc.GetString("character-exported", ("path", path));
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to export character");
            StatusText = _loc.GetString("character-export-failed");
        }
    }

    /// <summary>
    /// Rolls a random look: species, colours and one marking per layer that has any.
    /// </summary>
    /// <remarks>
    /// Meant as a starting point when the blank editor is intimidating, so it fills everything
    /// visible at once. Markings are picked per layer including "none", which keeps results from
    /// always looking like a costume party.
    /// </remarks>
    public void Randomize()
    {
        if (Species.Count == 0)
            return;

        _suppressRender = true;

        SelectedSpecies = Species[Random.Shared.Next(Species.Count)];
        Female = Random.Shared.Next(2) == 0;

        SkinTone = Random.Shared.Next(0, 101);
        SkinColorHex = RandomColor().ToHex();
        EyeColorHex = RandomColor().ToHex();

        foreach (var layer in Layers)
        {
            layer.SelectedMarking = layer.Options[Random.Shared.Next(layer.Options.Count)];
            layer.ColorHex = RandomColor().ToHex();
        }

        _suppressRender = false;
        QueueRender();
    }

    private static CharacterColor RandomColor()
    {
        return new CharacterColor(
            (byte) Random.Shared.Next(40, 256),
            (byte) Random.Shared.Next(40, 256),
            (byte) Random.Shared.Next(40, 256));
    }

    /// <summary>
    /// Writes the character the preview is showing to a temporary file, to hand to the clipboard.
    /// </summary>
    /// <returns>The file, or null when there is nothing drawn yet.</returns>
    public string? WriteToTemp()
    {
        if (_lastRender is not { } rendered)
            return null;

        try
        {
            // Named after the character so the pasted file is not called "tmp1234".
            var path = Path.Combine(Path.GetTempPath(), SuggestedFileName);
            rendered.Scale(ExportScale).SavePng(path);

            return path;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to write the character for the clipboard");

            return null;
        }
    }

    /// <summary>
    /// Builds the character in the form the game saves to its CHARACTERS folder.
    /// </summary>
    public CharacterProfile? BuildProfile()
    {
        if (SelectedSpecies is not { } species)
            return null;

        var markings = new List<ProfileMarking>();
        string? hair = null;
        string? facialHair = null;
        var hairColor = CharacterColor.White;
        var facialHairColor = CharacterColor.White;

        foreach (var layer in Layers)
        {
            if (layer.SelectedMarking?.Marking is not { } marking)
                continue;

            var color = CharacterColor.FromHex(layer.ColorHex);

            // Hair and beard have fields of their own in the file; everything else is a marking.
            switch (layer.BodyPart)
            {
                case "Hair":
                    hair = marking.Id;
                    hairColor = color;
                    break;

                case "FacialHair":
                    facialHair = marking.Id;
                    facialHairColor = color;
                    break;

                default:
                    // One colour per sprite layer, which is what the game expects to find.
                    markings.Add(new ProfileMarking(
                        marking.Id,
                        Enumerable.Repeat(color, Math.Max(1, marking.Sprites.Count)).ToList()));
                    break;
            }
        }

        return new CharacterProfile(
            string.IsNullOrWhiteSpace(CharacterName) ? "Urist McHands" : CharacterName.Trim(),
            species.Entry.Name,
            Age,
            Female,
            UsesSkinTone ? CharacterColor.FromSkinTone(SkinTone) : CharacterColor.FromHex(SkinColorHex),
            CharacterColor.FromHex(EyeColorHex),
            hair,
            hairColor,
            facialHair,
            facialHairColor,
            markings,
            SelectedJob?.Job?.Id,
            SelectedFork.Fork.ProfileForkId);
    }

    /// <summary>
    /// Writes the character out as a file the game can load.
    /// </summary>
    public void ExportProfile(string path)
    {
        if (BuildProfile() is not { } profile)
            return;

        try
        {
            File.WriteAllText(path, profile.ToYaml());
            StatusText = _loc.GetString("character-profile-exported", ("path", path));
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to export character profile");
            StatusText = _loc.GetString("character-export-failed");
        }
    }

    /// <summary>
    /// Loads a character file into the editor, as far as this fork's prototypes allow.
    /// </summary>
    public void ImportProfile(string path)
    {
        CharacterProfile profile;
        try
        {
            if (CharacterProfile.Parse(File.ReadAllText(path)) is not { } parsed)
            {
                StatusText = _loc.GetString("character-profile-import-invalid");
                return;
            }

            profile = parsed;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to read character profile");
            StatusText = _loc.GetString("character-profile-import-invalid");
            return;
        }

        Apply(profile);
    }

    /// <summary>
    /// Puts a loaded character into the editor, counting what this fork could not recognise.
    /// </summary>
    /// <summary>
    /// Draws a character file as the selected fork would, without touching the editor's state.
    /// Markings the fork lacks are left out rather than failing the picture.
    /// </summary>
    public async Task<Bitmap?> RenderProfileAsync(CharacterProfile profile)
    {
        if (!_catalogs.TryGetValue(SelectedFork.Fork.Id, out var catalog))
            return null;

        var species = catalog.Species.FirstOrDefault(s =>
            s.Name.Equals(profile.Species, StringComparison.OrdinalIgnoreCase));
        if (species == null)
            return null;

        var config = new CharacterConfig
        {
            Species = species,
            Female = profile.Female,
            SkinColor = profile.SkinColor,
            EyeColor = profile.EyeColor,
            Direction = RsiDirection.South,
        };

        void Put(string? markingId, CharacterColor color)
        {
            if (markingId == null)
                return;

            foreach (var (bodyPart, list) in catalog.Markings)
            {
                if (list.FirstOrDefault(m => m.Id == markingId) is { } marking)
                {
                    config.Markings[bodyPart] = new MarkingSelection(marking, color);
                    return;
                }
            }
        }

        Put(profile.Hair, profile.HairColor);
        Put(profile.FacialHair, profile.FacialHairColor);

        foreach (var marking in profile.Markings)
        {
            Put(marking.MarkingId, marking.Colors.FirstOrDefault(CharacterColor.White));
        }

        if (profile.JobId != null && catalog.Jobs.Jobs.FirstOrDefault(j => j.Id == profile.JobId) is { } job)
        {
            foreach (var (slot, item) in catalog.Jobs.OutfitFor(job))
            {
                config.Outfit[slot] = item;
            }
        }

        var rendered = await new CharacterRenderer(catalog).RenderAsync(config);

        return rendered?.Scale(3).ToBitmap();
    }

    /// <summary>
    /// Puts a character from the gallery into the editor.
    /// </summary>
    public void LoadProfile(CharacterProfile profile) => Apply(profile);

    private void Apply(CharacterProfile profile)
    {
        _suppressRender = true;

        CharacterName = profile.Name;
        Age = profile.Age;
        Female = profile.Female;

        if (Species.FirstOrDefault(s => s.Entry.Name.Equals(profile.Species, StringComparison.OrdinalIgnoreCase))
            is { } species)
        {
            SelectedSpecies = species;
        }

        SkinColorHex = profile.SkinColor.ToHex();
        EyeColorHex = profile.EyeColor.ToHex();

        var missing = 0;

        // Every layer starts empty so the file decides the whole look, not what was there before.
        foreach (var layer in Layers)
        {
            layer.SelectedMarking = layer.Options[0];
        }

        missing += ApplyMarking("Hair", profile.Hair, profile.HairColor);
        missing += ApplyMarking("FacialHair", profile.FacialHair, profile.FacialHairColor);

        foreach (var marking in profile.Markings)
        {
            missing += ApplyMarking(null, marking.MarkingId, marking.Colors.FirstOrDefault(CharacterColor.White));
        }

        if (profile.JobId != null && Jobs.FirstOrDefault(job => job.Job?.Id == profile.JobId) is { } jobOption)
            SelectedJob = jobOption;

        _suppressRender = false;
        QueueRender();

        StatusText = missing == 0
            ? _loc.GetString("character-profile-imported", ("name", profile.Name))
            : _loc.GetString("character-profile-imported-partial", ("name", profile.Name), ("missing", missing));
    }

    /// <summary>
    /// Selects a marking by prototype id, on a known layer or wherever it turns up.
    /// </summary>
    /// <returns>1 if this fork has no such marking, 0 otherwise.</returns>
    private int ApplyMarking(string? bodyPart, string? markingId, CharacterColor color)
    {
        if (markingId == null)
            return 0;

        foreach (var layer in Layers)
        {
            if (bodyPart != null && layer.BodyPart != bodyPart)
                continue;

            if (layer.Options.FirstOrDefault(option => option.Marking?.Id == markingId) is not { } option)
                continue;

            layer.SelectedMarking = option;
            layer.ColorHex = color.ToHex();

            return 0;
        }

        Log.Debug("Character file uses {Marking}, which this fork does not have", markingId);

        return 1;
    }

    /// <summary>
    /// Suggested file name for the export dialog.
    /// </summary>
    public string SuggestedFileName =>
        $"{SelectedFork.Fork.Id}-{SelectedSpecies?.Entry.Name ?? "character"}-{(Female ? "f" : "m")}.png";

    /// <summary>
    /// Suggested file name for the character file, which the game names after the character.
    /// </summary>
    public string SuggestedProfileFileName =>
        $"{(string.IsNullOrWhiteSpace(CharacterName) ? "character" : CharacterName.Trim())}.yml";

    /// <summary>
    /// Where the game keeps character files for this build. Created on first use, so saving works
    /// before the game has ever been launched from here.
    /// </summary>
    public static string CharactersDirectory
    {
        get
        {
            var path = Path.Combine(LauncherPaths.DirClientData, CharacterProfile.CharactersFolder);
            Directory.CreateDirectory(path);

            return path;
        }
    }

    /// <summary>
    /// Saves the character straight into the game's characters folder, named the way the game
    /// names it, so it shows up in the game's own list next time it starts.
    /// </summary>
    public void SaveProfile()
    {
        if (BuildProfile() is not { } profile)
            return;

        var fileName = SafeFileName(profile.Name) + ".yml";
        ExportProfile(Path.Combine(CharactersDirectory, fileName));
    }

    /// <summary>
    /// A character name as a file name: anything the file system objects to becomes an underscore.
    /// </summary>
    public static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();

        return cleaned.Length == 0 ? "character" : cleaned;
    }

    public bool CanExport => _lastRender != null;

    /// <summary>
    /// Throws away this fork's downloaded sprites and fetches them again.
    /// </summary>
    public void RefreshPressed()
    {
        var fork = SelectedFork.Fork;

        if (_catalogs.Remove(fork.Id, out var catalog))
            catalog.Resources.Clear();
        else
            new ForkResources(fork, _http).Clear();

        Species.Clear();
        Layers.Clear();
        Preview = null;
        _lastRender = null;

        _ = LoadForkAsync();
    }
}

/// <summary>
/// One content repository in the fork drop-down.
/// </summary>
public sealed class ForkOptionViewModel(CharacterFork fork)
{
    public CharacterFork Fork { get; } = fork;

    public string Name => fork.DisplayName;

    public override string ToString() => Name;
}

/// <summary>
/// One species in the species drop-down, named the way the fork names it.
/// </summary>
public sealed class SpeciesOptionViewModel(SpeciesEntry entry, string name)
{
    public SpeciesEntry Entry { get; } = entry;

    public string Name { get; } = name;

    public override string ToString() => Name;
}

/// <summary>
/// One job in the job drop-down; a null job means no uniform.
/// </summary>
public sealed class JobOptionViewModel(JobPrototype? job, string name)
{
    public JobPrototype? Job { get; } = job;

    public string Name { get; } = name;

    public override string ToString() => Name;
}

/// <summary>
/// One facing in the direction drop-down.
/// </summary>
public sealed class DirectionOptionViewModel(RsiDirection direction, LocalizationManager loc)
{
    public RsiDirection Direction { get; } = direction;

    public string Name => loc.GetString($"character-direction-{direction.ToString().ToLowerInvariant()}");

    public override string ToString() => Name;
}

/// <summary>
/// One marking layer: what can go on it, what is on it, and in what colour.
/// </summary>
public sealed class CharacterLayerViewModel : ReactiveObject
{
    private readonly Action _changed;

    public string BodyPart { get; }

    public string DisplayName { get; }

    public ObservableCollection<MarkingOptionViewModel> Options { get; } = new();

    public CharacterLayerViewModel(
        string bodyPart,
        IReadOnlyList<MarkingPrototype> markings,
        LocalizationManager loc,
        ForkLocale forkLocale,
        Action changed)
    {
        BodyPart = bodyPart;
        _changed = changed;

        var name = loc.GetString($"character-layer-{bodyPart.ToLowerInvariant()}");
        DisplayName = name == $"character-layer-{bodyPart.ToLowerInvariant()}" ? bodyPart : name;

        Options.Add(new MarkingOptionViewModel(null, loc.GetString("character-marking-none")) { Layer = this });

        foreach (var marking in markings)
        {
            // The fork's own wording where it has one, the prototype id split up where it doesn't.
            Options.Add(new MarkingOptionViewModel(
                marking,
                forkLocale.Get($"marking-{marking.Id}") ?? marking.DisplayName) { Layer = this });
        }

        _selectedMarking = Options[0];
    }

    private MarkingOptionViewModel _selectedMarking;

    public MarkingOptionViewModel SelectedMarking
    {
        get => _selectedMarking;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedMarking, value);
            _changed();
        }
    }

    private string _colorHex = "#FFFFFF";

    public string ColorHex
    {
        get => _colorHex;
        set
        {
            this.RaiseAndSetIfChanged(ref _colorHex, value);
            _changed();
        }
    }
}

/// <summary>
/// One entry in a marking drop-down; a null marking means "nothing on this layer".
/// </summary>
public sealed class MarkingOptionViewModel(MarkingPrototype? marking, string name)
{
    public MarkingPrototype? Marking { get; } = marking;

    public string Name { get; } = name;

    /// <summary>
    /// The layer this option belongs to, so hovering it knows what it would replace.
    /// </summary>
    public CharacterLayerViewModel? Layer { get; init; }

    public override string ToString() => Name;
}
