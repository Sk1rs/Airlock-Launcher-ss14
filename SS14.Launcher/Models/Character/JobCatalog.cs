using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;
using YamlDotNet.RepresentationModel;

namespace SS14.Launcher.Models.Character;

/// <summary>
/// A job you can put a character in.
/// </summary>
/// <param name="Id">Prototype id, e.g. <c>StationEngineer</c>.</param>
/// <param name="StartingGear">The gear prototype it hands out, if it names one.</param>
/// <param name="SourceFile">Where it was defined, used to find its matching loadout file.</param>
public sealed record JobPrototype(string Id, string? StartingGear, string SourceFile, string? NameKey = null)
{
    /// <summary>
    /// Job ids are one CamelCase word; split them so they read as a name.
    /// </summary>
    public string DisplayName => System.Text.RegularExpressions.Regex
        .Replace(Id, "(?<!^)([A-Z])", " $1");
}

/// <summary>
/// Works out what a job wears.
/// </summary>
/// <remarks>
/// Outfits come from two places these days. A job's <c>startingGear</c> still carries the bits that
/// are not up to the player (a headset, a belt), while the uniform itself moved into the loadout
/// system, where each slot is a menu of alternatives. There is no single "the" outfit any more, so
/// this takes the gear as given and fills the empty slots with the first alternative each loadout
/// file offers, which is what the job looks like in the lobby before anyone changes anything.
/// </remarks>
public sealed class JobCatalog
{
    private readonly List<JobPrototype> _jobs = [];

    /// <summary>
    /// Equipment by prototype id, covering both <c>startingGear</c> and <c>loadout</c>.
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, string>> _gear = new();

    /// <summary>
    /// Loadout equipment grouped by the file it came from, in file order.
    /// </summary>
    private readonly Dictionary<string, List<Dictionary<string, string>>> _loadoutsByFile = new();

    public IReadOnlyList<JobPrototype> Jobs => _jobs;

    /// <summary>
    /// Reads one prototype file, picking up jobs, starting gear and loadouts alike.
    /// </summary>
    public void AddFile(string path, string yaml)
    {
        YamlStream stream;
        try
        {
            stream = new YamlStream();
            stream.Load(new StringReader(yaml));
        }
        catch (Exception e)
        {
            Log.Debug(e, "Skipping unparseable job prototype file {Path}", path);
            return;
        }

        foreach (var document in stream.Documents)
        {
            if (document.RootNode is not YamlSequenceNode root)
                continue;

            foreach (var node in root.Children.OfType<YamlMappingNode>())
            {
                var type = Scalar(node, "type");
                var id = Scalar(node, "id");

                switch (type)
                {
                    case "job" when id != null:
                        _jobs.Add(new JobPrototype(id, Scalar(node, "startingGear"), path, Scalar(node, "name")));
                        break;

                    case "startingGear" or "loadout":
                    {
                        var equipment = ParseEquipment(node);
                        if (equipment.Count == 0)
                            break;

                        if (id != null)
                            _gear[id] = equipment;

                        if (!_loadoutsByFile.TryGetValue(path, out var list))
                            _loadoutsByFile[path] = list = [];

                        list.Add(equipment);
                        break;
                    }
                }
            }
        }
    }

    public void SortJobs()
    {
        _jobs.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The slots a job fills and the item prototype in each.
    /// </summary>
    public Dictionary<string, string> OutfitFor(JobPrototype job)
    {
        var outfit = new Dictionary<string, string>();

        if (job.StartingGear != null && _gear.TryGetValue(job.StartingGear, out var gear))
        {
            foreach (var (slot, item) in gear)
            {
                outfit[slot] = item;
            }
        }

        foreach (var equipment in LoadoutsFor(job))
        {
            foreach (var (slot, item) in equipment)
            {
                // First offer wins: the lists are written best-known-first.
                outfit.TryAdd(slot, item);
            }
        }

        return outfit;
    }

    /// <summary>
    /// Loadout entries belonging to a job, matched by file name.
    /// </summary>
    /// <remarks>
    /// Jobs and their loadouts live in files of the same name under different folders, which is the
    /// only link between them that does not need the whole loadout group system resolved.
    /// </remarks>
    private IEnumerable<Dictionary<string, string>> LoadoutsFor(JobPrototype job)
    {
        var name = Path.GetFileName(job.SourceFile);

        return _loadoutsByFile
            .Where(pair => Path.GetFileName(pair.Key) == name)
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .SelectMany(pair => pair.Value);
    }

    private static Dictionary<string, string> ParseEquipment(YamlMappingNode node)
    {
        var equipment = new Dictionary<string, string>();

        if (!node.Children.TryGetValue(new YamlScalarNode("equipment"), out var value)
            || value is not YamlMappingNode mapping)
        {
            return equipment;
        }

        foreach (var (slot, item) in mapping.Children)
        {
            if (slot is YamlScalarNode { Value: { } slotName } && item is YamlScalarNode { Value: { } itemId })
                equipment[slotName] = itemId;
        }

        return equipment;
    }

    private static string? Scalar(YamlMappingNode node, string key)
    {
        return node.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlScalarNode scalar
            ? scalar.Value
            : null;
    }
}
