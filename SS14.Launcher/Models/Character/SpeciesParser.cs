using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;
using YamlDotNet.RepresentationModel;

namespace SS14.Launcher.Models.Character;

/// <summary>
/// What a species prototype tells us: what it is called and how its skin is coloured.
/// </summary>
/// <param name="Id">Prototype id, e.g. <c>Human</c>.</param>
/// <param name="NameKey">Translation key for its name, e.g. <c>species-name-human</c>.</param>
/// <param name="SkinColoration">How the game tints it, e.g. <c>HumanToned</c>.</param>
public sealed record SpeciesPrototype(string Id, string? NameKey, string SkinColoration);

public static class SpeciesParser
{
    /// <summary>
    /// Reads every <c>type: species</c> document in a prototype file.
    /// </summary>
    public static List<SpeciesPrototype> Parse(string yaml)
    {
        var species = new List<SpeciesPrototype>();

        YamlStream stream;
        try
        {
            stream = new YamlStream();
            stream.Load(new StringReader(yaml));
        }
        catch (Exception e)
        {
            Log.Debug(e, "Skipping unparseable species prototype file");
            return species;
        }

        foreach (var document in stream.Documents)
        {
            if (document.RootNode is not YamlSequenceNode root)
                continue;

            foreach (var node in root.Children.OfType<YamlMappingNode>())
            {
                if (Scalar(node, "type") != "species" || Scalar(node, "id") is not { } id)
                    continue;

                species.Add(new SpeciesPrototype(
                    id,
                    Scalar(node, "name"),
                    Scalar(node, "skinColoration") ?? "Hues"));
            }
        }

        return species;
    }

    private static string? Scalar(YamlMappingNode node, string key)
    {
        return node.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlScalarNode scalar
            ? scalar.Value
            : null;
    }
}
