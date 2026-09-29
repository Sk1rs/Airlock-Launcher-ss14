using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;
using YamlDotNet.RepresentationModel;

namespace SS14.Launcher.Models.Character;

/// <summary>
/// One customisation option: a hairstyle, a snout, a tail, a tattoo.
/// </summary>
/// <param name="Id">Prototype id, e.g. <c>HumanHairAfro</c>.</param>
/// <param name="BodyPart">Layer it is drawn on, e.g. <c>Hair</c>, <c>Snout</c>, <c>Tail</c>.</param>
/// <param name="Sprites">RSI path and state per sub-layer; several means several colourable parts.</param>
/// <param name="SpeciesRestriction">Species ids this is limited to, empty if unrestricted.</param>
/// <param name="GroupWhitelist">Species groups this is limited to, empty if unrestricted.</param>
/// <param name="FollowsSkinColor">Drawn in the skin colour rather than a colour of its own.</param>
public sealed record MarkingPrototype(
    string Id,
    string BodyPart,
    IReadOnlyList<MarkingSprite> Sprites,
    IReadOnlyList<string> SpeciesRestriction,
    IReadOnlyList<string> GroupWhitelist,
    bool FollowsSkinColor)
{
    /// <summary>
    /// A readable name: prototype ids are written as one long CamelCase word.
    /// </summary>
    public string DisplayName => SplitCamelCase(Id);

    public bool AllowedFor(string species)
    {
        // A marking with neither list is fair game; the group whitelist names species groups we
        // can't resolve without more prototypes, so treat a group named after the species as a hit.
        if (SpeciesRestriction.Count > 0 && SpeciesRestriction.Contains(species))
            return true;

        if (GroupWhitelist.Count > 0 && GroupWhitelist.Contains(species))
            return true;

        return SpeciesRestriction.Count == 0 && GroupWhitelist.Count == 0;
    }

    private static string SplitCamelCase(string value)
    {
        var result = new System.Text.StringBuilder(value.Length + 8);

        for (var i = 0; i < value.Length; i++)
        {
            if (i > 0 && char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
                result.Append(' ');

            result.Append(value[i]);
        }

        return result.ToString();
    }
}

public sealed record MarkingSprite(string Rsi, string State);

public static class MarkingParser
{
    /// <summary>
    /// Reads every <c>type: marking</c> document out of a prototype file.
    /// </summary>
    /// <remarks>
    /// Deliberately hand-rolled over the YAML node tree instead of deserializing into types: fork
    /// prototypes carry extra fields and custom tags like <c>!type:SkinColoring</c> that a typed
    /// deserializer would choke on, and everything we need is three plain keys deep.
    /// </remarks>
    public static List<MarkingPrototype> Parse(string yaml)
    {
        var markings = new List<MarkingPrototype>();

        YamlStream stream;
        try
        {
            stream = new YamlStream();
            stream.Load(new StringReader(yaml));
        }
        catch (Exception e)
        {
            Log.Debug(e, "Skipping unparseable marking prototype file");
            return markings;
        }

        foreach (var document in stream.Documents)
        {
            if (document.RootNode is not YamlSequenceNode root)
                continue;

            foreach (var entry in root.Children.OfType<YamlMappingNode>())
            {
                if (GetScalar(entry, "type") != "marking")
                    continue;

                var id = GetScalar(entry, "id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var sprites = ParseSprites(entry);
                if (sprites.Count == 0)
                    continue;

                markings.Add(new MarkingPrototype(
                    id,
                    GetScalar(entry, "bodyPart") ?? "Special",
                    sprites,
                    GetStringList(entry, "speciesRestriction"),
                    GetStringList(entry, "groupWhitelist"),
                    GetScalar(entry, "followSkinColor") == "true"));
            }
        }

        return markings;
    }

    private static List<MarkingSprite> ParseSprites(YamlMappingNode entry)
    {
        var sprites = new List<MarkingSprite>();

        if (!entry.Children.TryGetValue(new YamlScalarNode("sprites"), out var node)
            || node is not YamlSequenceNode sequence)
        {
            return sprites;
        }

        foreach (var sprite in sequence.Children.OfType<YamlMappingNode>())
        {
            var rsi = GetScalar(sprite, "sprite");
            var state = GetScalar(sprite, "state");

            if (rsi != null && state != null)
                sprites.Add(new MarkingSprite(rsi, state));
        }

        return sprites;
    }

    private static string? GetScalar(YamlMappingNode node, string key)
    {
        return node.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlScalarNode scalar
            ? scalar.Value
            : null;
    }

    private static List<string> GetStringList(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var value) || value is not YamlSequenceNode sequence)
            return [];

        return sequence.Children
            .OfType<YamlScalarNode>()
            .Select(scalar => scalar.Value)
            .Where(scalar => scalar != null)
            .ToList()!;
    }
}
