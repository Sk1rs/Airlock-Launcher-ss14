using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace SS14.Launcher.Models.Character;

/// <summary>
/// What a given fork offers the character editor: which species exist and which markings can go on them.
/// </summary>
public sealed partial class CharacterCatalog
{
    /// <summary>
    /// Where a species keeps its body sprites, e.g.
    /// <c>Resources/Textures/Mobs/Species/Human/parts.rsi/meta.json</c>.
    /// </summary>
    [GeneratedRegex(@"^Resources/Textures/Mobs/Species/([^/]+)/parts\.rsi/meta\.json$")]
    private static partial Regex SpeciesPartsRegex { get; }

    /// <summary>
    /// Marking prototypes. Forks add their own folders, so this matches on the path shape rather
    /// than a fixed list of files.
    /// </summary>
    [GeneratedRegex(@"^Resources/Prototypes/.*(Markings|Customization).*\.yml$", RegexOptions.IgnoreCase)]
    private static partial Regex MarkingFileRegex { get; }

    [GeneratedRegex(@"^Resources/Prototypes/Species/[^/]+\.yml$")]
    private static partial Regex SpeciesPrototypeRegex { get; }

    /// <summary>
    /// Job definitions, and the loadouts that go with them.
    /// </summary>
    [GeneratedRegex(@"^Resources/Prototypes/(Roles/Jobs|Loadouts)/.*\.yml$")]
    private static partial Regex JobFileRegex { get; }

    /// <summary>
    /// Clothing prototypes, which is where an outfit's item ids turn into sprites.
    /// </summary>
    [GeneratedRegex(@"^Resources/Prototypes/Entities/Clothing/.*\.yml$")]
    private static partial Regex ClothingFileRegex { get; }

    private readonly ForkResources _resources;

    /// <summary>
    /// Whether to fetch files many at a time. The slow path is kept for connections that dislike
    /// a burst of parallel requests.
    /// </summary>
    public bool FastDownload { get; set; } = true;

    /// <summary>
    /// Launcher language to look for in the fork's own translations.
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// The fork's names for species, markings and jobs.
    /// </summary>
    public ForkLocale Locale { get; private set; } = new();

    public CharacterCatalog(ForkResources resources)
    {
        _resources = resources;
    }

    public ForkResources Resources => _resources;

    /// <summary>
    /// Species that have body sprites in this fork, alphabetically.
    /// </summary>
    public IReadOnlyList<SpeciesEntry> Species { get; private set; } = [];

    /// <summary>
    /// Every marking prototype found, keyed by the body layer it goes on.
    /// </summary>
    public IReadOnlyDictionary<string, List<MarkingPrototype>> Markings { get; private set; } =
        new Dictionary<string, List<MarkingPrototype>>();

    /// <summary>
    /// Jobs and the outfits they come with. Empty when the repository listing was unavailable.
    /// </summary>
    public JobCatalog Jobs { get; private set; } = new();

    /// <summary>
    /// Clothing prototypes, for turning an outfit's item ids into sprites.
    /// </summary>
    public EntityPrototypeIndex Clothing { get; private set; } = new();

    /// <summary>
    /// True when the repository's file list was unavailable and only well-known paths were tried,
    /// so anything a fork added of its own is missing.
    /// </summary>
    public bool LimitedDiscovery { get; private set; }

    /// <summary>
    /// Downloads (or reads from cache) everything needed to populate the editor's drop-downs.
    /// </summary>
    public async Task LoadAsync(IProgress<string>? progress = null, CancellationToken cancel = default)
    {
        var listed = await _resources.GetFileListAsync(cancel);

        LimitedDiscovery = listed == null;
        var files = listed ?? await ProbeKnownPathsAsync(cancel);

        progress?.Report("species");
        Species = await LoadSpeciesAsync(files, cancel);

        progress?.Report("markings");
        Markings = await LoadMarkingsAsync(files, cancel);

        // Outfits need to know every clothing prototype in the fork, and there is no way to search
        // for one by id, so that step only works when the repository listing came through.
        if (!LimitedDiscovery)
        {
            progress?.Report("jobs");
            await LoadJobsAsync(files, cancel);
        }

        // Names work either way: the locale files sit at paths every fork inherits.
        progress?.Report("names");
        var locale = new ForkLocale();
        await locale.LoadAsync(_resources, files, Language, FastDownload, cancel);
        Locale = locale;

        Log.Information(
            "Character catalog for {Fork}: {Species} species, {Markings} markings",
            _resources.Fork.Id,
            Species.Count,
            Markings.Values.Sum(list => list.Count));
    }

    /// <summary>
    /// Builds a stand-in file list by asking for well-known paths and keeping the ones that answer.
    /// </summary>
    private async Task<string[]> ProbeKnownPathsAsync(CancellationToken cancel)
    {
        var found = new List<string>();

        foreach (var species in KnownPaths.SpeciesFolders)
        {
            cancel.ThrowIfCancellationRequested();

            var path = $"Resources/Textures/Mobs/Species/{species}/parts.rsi/meta.json";
            if (await _resources.GetFileAsync(path, cancel) != null)
                found.Add(path);
        }

        var prototypes = KnownPaths.SpeciesPrototypes
            .Concat(KnownPaths.MarkingPrototypes)
            .Select(prototype => $"Resources/Prototypes/{prototype}")
            .ToArray();

        found.AddRange((await _resources.GetTextFilesAsync(prototypes, FastDownload, cancel: cancel)).Keys);

        Log.Information("Probed {Count} known paths in {Fork}", found.Count, _resources.Fork.Id);

        return found.ToArray();
    }

    private async Task<List<SpeciesEntry>> LoadSpeciesAsync(string[] files, CancellationToken cancel)
    {
        var species = new List<SpeciesEntry>();

        // The sprites are what decide whether a species can be drawn at all; its prototype only
        // adds a name and how its skin is coloured.
        var prototypes = await LoadSpeciesPrototypesAsync(files, cancel);

        foreach (var file in files)
        {
            if (SpeciesPartsRegex.Match(file) is not { Success: true } match)
                continue;

            var name = match.Groups[1].Value;
            var meta = await LoadMetaAsync($"Resources/Textures/Mobs/Species/{name}/parts.rsi", cancel);
            if (meta == null)
                continue;

            var prototype = prototypes.GetValueOrDefault(name);

            species.Add(new SpeciesEntry(
                name,
                $"Mobs/Species/{name}/parts.rsi",
                meta,
                prototype?.SkinColoration ?? "Hues",
                prototype?.NameKey));
        }

        return species.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<Dictionary<string, SpeciesPrototype>> LoadSpeciesPrototypesAsync(
        string[] files,
        CancellationToken cancel)
    {
        var prototypes = new Dictionary<string, SpeciesPrototype>(StringComparer.OrdinalIgnoreCase);

        var paths = files.Where(file => SpeciesPrototypeRegex.IsMatch(file)).ToArray();

        foreach (var text in (await _resources.GetTextFilesAsync(paths, FastDownload, cancel: cancel)).Values)
        {
            foreach (var species in SpeciesParser.Parse(text))
            {
                prototypes[species.Id] = species;
            }
        }

        return prototypes;
    }

    private async Task<Dictionary<string, List<MarkingPrototype>>> LoadMarkingsAsync(
        string[] files,
        CancellationToken cancel)
    {
        var byBodyPart = new Dictionary<string, List<MarkingPrototype>>();

        var markingFiles = files.Where(file => MarkingFileRegex.IsMatch(file)).ToArray();
        var texts = await _resources.GetTextFilesAsync(markingFiles, FastDownload, cancel: cancel);

        foreach (var text in texts.Values)
        {
            foreach (var marking in MarkingParser.Parse(text))
            {
                if (!byBodyPart.TryGetValue(marking.BodyPart, out var list))
                    byBodyPart[marking.BodyPart] = list = [];

                list.Add(marking);
            }
        }

        foreach (var list in byBodyPart.Values)
        {
            list.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
        }

        return byBodyPart;
    }

    /// <summary>
    /// Reads jobs, their gear and every clothing prototype the fork ships.
    /// </summary>
    private async Task LoadJobsAsync(string[] files, CancellationToken cancel)
    {
        var jobs = new JobCatalog();
        var clothing = new EntityPrototypeIndex();

        var wanted = files.Where(file => JobFileRegex.IsMatch(file) || ClothingFileRegex.IsMatch(file)).ToArray();
        var texts = await _resources.GetTextFilesAsync(wanted, FastDownload, cancel: cancel);

        // Jobs are added in path order so a job always meets its own loadout file the same way.
        foreach (var file in wanted.OrderBy(file => file, StringComparer.Ordinal))
        {
            if (!texts.TryGetValue(file, out var yaml))
                continue;

            if (JobFileRegex.IsMatch(file))
                jobs.AddFile(file, yaml);
            else
                clothing.AddFile(yaml);
        }

        jobs.SortJobs();

        Jobs = jobs;
        Clothing = clothing;

        Log.Information(
            "Loaded {Jobs} jobs and {Clothing} clothing prototypes for {Fork}",
            jobs.Jobs.Count,
            clothing.Count,
            _resources.Fork.Id);
    }

    /// <summary>
    /// Loads and caches the description of one sprite sheet folder.
    /// </summary>
    public async Task<RsiMeta?> LoadMetaAsync(string rsiPath, CancellationToken cancel = default)
    {
        var text = await _resources.GetTextAsync($"Resources/Textures/{Strip(rsiPath)}/meta.json", cancel);

        if (text == null)
            return null;

        try
        {
            return RsiMeta.Parse(text);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Unreadable RSI metadata at {Path}", rsiPath);
            return null;
        }
    }

    /// <summary>
    /// Marking prototypes that this species is allowed to wear on the given layer.
    /// </summary>
    public IReadOnlyList<MarkingPrototype> MarkingsFor(string bodyPart, string species)
    {
        if (!Markings.TryGetValue(bodyPart, out var list))
            return [];

        return list.Where(marking => marking.AllowedFor(species)).ToList();
    }

    /// <summary>
    /// Prototypes name sprite folders relative to Textures, the file list is repository-relative.
    /// </summary>
    internal static string Strip(string rsiPath)
    {
        const string prefix = "Resources/Textures/";

        return rsiPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? rsiPath[prefix.Length..]
            : rsiPath;
    }
}

/// <summary>
/// A species the editor can draw.
/// </summary>
/// <param name="Name">Folder and prototype name, e.g. <c>Human</c>.</param>
/// <param name="PartsRsi">Sprite folder holding its body parts, relative to Textures.</param>
/// <param name="Parts">What that folder contains.</param>
/// <param name="SkinColoration">How the game tints its skin: <c>HumanToned</c>, <c>Hues</c>, and so on.</param>
public sealed record SpeciesEntry(
    string Name,
    string PartsRsi,
    RsiMeta Parts,
    string SkinColoration,
    string? NameKey = null)
{
    public bool UsesSkinTone => SkinColoration.Equals("HumanToned", StringComparison.OrdinalIgnoreCase);
}
