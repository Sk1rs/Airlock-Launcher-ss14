using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Serilog;
using YamlDotNet.RepresentationModel;

namespace SS14.Launcher.Models.Character;

/// <summary>
/// One marking as a character file records it: what it is and a colour per sprite layer.
/// </summary>
public sealed record ProfileMarking(string MarkingId, IReadOnlyList<CharacterColor> Colors);

/// <summary>
/// A character in the format the game itself saves to <c>data/CHARACTERS</c>.
/// </summary>
/// <remarks>
/// All three forks write the same document shape but not the same contents: one adds hair gradients,
/// another adds its own appearance fields and a far richer loadout block. Anything a fork does not
/// recognise it ignores, and anything it expects but does not find it fills in itself, so writing
/// the fields they all share produces a file every one of them can open. Unknown keys are likewise
/// carried over untouched when a file is read back, so a round trip loses nothing.
/// </remarks>
public sealed record CharacterProfile(
    string Name,
    string Species,
    int Age,
    bool Female,
    CharacterColor SkinColor,
    CharacterColor EyeColor,
    string? Hair,
    CharacterColor HairColor,
    string? FacialHair,
    CharacterColor FacialHairColor,
    IReadOnlyList<ProfileMarking> Markings,
    string? JobId,
    string? ForkId)
{
    /// <summary>
    /// Where the game keeps these files, under its own data directory.
    /// </summary>
    public const string CharactersFolder = "CHARACTERS";

    /// <summary>
    /// Writes the character out the way the game does.
    /// </summary>
    public string ToYaml()
    {
        var sex = Female ? "Female" : "Male";
        var text = new StringBuilder();

        text.AppendLine("profile:");
        text.AppendLine("  preferenceUnavailable: SpawnAsOverflow");
        text.AppendLine("  spawnPriority: None");
        text.AppendLine($"  name: {Quote(Name)}");
        text.AppendLine("  flavorText: \"\"");
        text.AppendLine($"  species: {Species}");
        text.AppendLine($"  age: {Age}");
        text.AppendLine($"  sex: {sex}");
        text.AppendLine($"  gender: {sex}");
        text.AppendLine("  appearance:");

        if (Markings.Count == 0)
        {
            text.AppendLine("    markings: []");
        }
        else
        {
            text.AppendLine("    markings:");
            foreach (var marking in Markings)
            {
                text.AppendLine($"    - markingId: {marking.MarkingId}");
                text.AppendLine("      visible: True");
                text.AppendLine("      markingColor:");

                foreach (var color in marking.Colors)
                {
                    text.AppendLine($"      - '{color.ToProfileHex()}'");
                }
            }
        }

        text.AppendLine($"    skinColor: '{SkinColor.ToProfileHex()}'");
        text.AppendLine($"    eyeColor: '{EyeColor.ToProfileHex()}'");
        text.AppendLine($"    hair: {Hair ?? "null"}");
        text.AppendLine($"    hairColor: '{HairColor.ToProfileHex()}'");
        text.AppendLine($"    facialHair: {FacialHair ?? "null"}");
        text.AppendLine($"    facialHairColor: '{FacialHairColor.ToProfileHex()}'");

        if (JobId == null)
        {
            text.AppendLine("  _jobPriorities: {}");
        }
        else
        {
            text.AppendLine("  _jobPriorities:");
            text.AppendLine($"    {JobId}: High");
        }

        text.AppendLine("  _antagPreferences: []");
        text.AppendLine("  _traitPreferences: []");
        text.AppendLine("  _loadouts: {}");
        text.AppendLine("version: 1");
        text.AppendLine($"forkId: {ForkId ?? "custom"}");
        text.AppendLine("...");

        return text.ToString();
    }

    /// <summary>
    /// Reads a character file, whichever fork wrote it.
    /// </summary>
    /// <returns>Null if the file is not a character at all.</returns>
    public static CharacterProfile? Parse(string yaml)
    {
        YamlStream stream;
        try
        {
            stream = new YamlStream();
            stream.Load(new StringReader(yaml));
        }
        catch (Exception e)
        {
            Log.Debug(e, "Character file is not readable YAML");
            return null;
        }

        var root = stream.Documents.FirstOrDefault()?.RootNode as YamlMappingNode;
        if (root == null || Node(root, "profile") is not YamlMappingNode profile)
            return null;

        var appearance = Node(profile, "appearance") as YamlMappingNode;

        return new CharacterProfile(
            Scalar(profile, "name") ?? "",
            Scalar(profile, "species") ?? "Human",
            int.TryParse(Scalar(profile, "age"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var age)
                ? age
                : 18,
            // Forks add sexes of their own; anything that is not plainly male is drawn female here.
            !string.Equals(Scalar(profile, "sex"), "Male", StringComparison.OrdinalIgnoreCase),
            Color(appearance, "skinColor", CharacterColor.FromSkinTone(20)),
            Color(appearance, "eyeColor", new CharacterColor(120, 80, 60)),
            Prototype(appearance, "hair"),
            Color(appearance, "hairColor", CharacterColor.White),
            Prototype(appearance, "facialHair"),
            Color(appearance, "facialHairColor", CharacterColor.White),
            ParseMarkings(appearance),
            ParseJob(profile),
            Scalar(root, "forkId"));
    }

    private static List<ProfileMarking> ParseMarkings(YamlMappingNode? appearance)
    {
        var markings = new List<ProfileMarking>();

        if (appearance == null || Node(appearance, "markings") is not YamlSequenceNode sequence)
            return markings;

        foreach (var node in sequence.Children.OfType<YamlMappingNode>())
        {
            if (Scalar(node, "markingId") is not { } id)
                continue;

            var colors = new List<CharacterColor>();

            // Forks disagree on the key: one writes it singular, another plural.
            var list = Node(node, "markingColor") as YamlSequenceNode
                       ?? Node(node, "markingColors") as YamlSequenceNode;

            if (list != null)
            {
                colors.AddRange(list.Children
                    .OfType<YamlScalarNode>()
                    .Where(scalar => scalar.Value != null)
                    .Select(scalar => CharacterColor.FromHex(scalar.Value!)));
            }

            if (colors.Count == 0)
                colors.Add(CharacterColor.White);

            markings.Add(new ProfileMarking(id, colors));
        }

        return markings;
    }

    /// <summary>
    /// The job the character is most keen on, which is the closest thing to "their" job.
    /// </summary>
    private static string? ParseJob(YamlMappingNode profile)
    {
        if (Node(profile, "_jobPriorities") is not YamlMappingNode priorities)
            return null;

        string? first = null;

        foreach (var (key, value) in priorities.Children)
        {
            if (key is not YamlScalarNode { Value: { } job } || value is not YamlScalarNode { Value: { } priority })
                continue;

            if (priority.Equals("High", StringComparison.OrdinalIgnoreCase))
                return job;

            first ??= job;
        }

        return first;
    }

    /// <summary>
    /// A prototype id, treating the file's explicit nulls as "nothing here".
    /// </summary>
    private static string? Prototype(YamlMappingNode? node, string key)
    {
        var value = Scalar(node, key);

        return string.IsNullOrWhiteSpace(value) || value == "null" ? null : value;
    }

    private static CharacterColor Color(YamlMappingNode? node, string key, CharacterColor fallback)
    {
        return Scalar(node, key) is { } value ? CharacterColor.FromHex(value) : fallback;
    }

    private static YamlNode? Node(YamlMappingNode? node, string key)
    {
        return node != null && node.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;
    }

    private static string? Scalar(YamlMappingNode? node, string key)
    {
        return Node(node, key) is YamlScalarNode scalar ? scalar.Value : null;
    }

    /// <summary>
    /// Names can hold anything, so they always go out quoted.
    /// </summary>
    private static string Quote(string value)
    {
        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }
}
