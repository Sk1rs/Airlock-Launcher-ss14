using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;
using YamlDotNet.RepresentationModel;

namespace SS14.Launcher.Models.Character;

/// <summary>
/// Just enough of an entity prototype to find out what a piece of clothing looks like when worn.
/// </summary>
/// <param name="Id">Prototype id, e.g. <c>ClothingUniformJumpsuitEngineering</c>.</param>
/// <param name="Parents">Prototypes it inherits from; a prototype can have several.</param>
/// <param name="Sprite">Sprite folder it declares, if any.</param>
public sealed record ClothingEntity(string Id, IReadOnlyList<string> Parents, string? Sprite);

/// <summary>
/// Looks up which sprite folder a clothing prototype uses, following inheritance.
/// </summary>
/// <remarks>
/// Most garments name their sprite outright. The ones that don't (plain coloured shoes and the
/// like) inherit it from a base prototype, so unresolved lookups walk up the parent chain.
/// </remarks>
public sealed class EntityPrototypeIndex
{
    private readonly Dictionary<string, ClothingEntity> _entities = new();

    public int Count => _entities.Count;

    public void AddFile(string yaml)
    {
        foreach (var entity in ParseEntities(yaml))
        {
            // First definition wins; forks occasionally redefine an id and we have no merge rules.
            _entities.TryAdd(entity.Id, entity);
        }
    }

    /// <summary>
    /// Suffixes marking a variant that only differs by what is inside it: combat boots with a
    /// knife in them are still combat boots, and the version carrying the sprite is the plain one.
    /// These variants are often defined away from the clothing prototypes, so falling back on the
    /// base name keeps such items from turning invisible.
    /// </summary>
    private static readonly string[] FilledSuffixes = ["Filled", "Full"];

    /// <summary>
    /// The sprite folder for a prototype, looked up through its parents.
    /// </summary>
    public string? ResolveSprite(string id)
    {
        if (ResolveSprite(id, depth: 0) is { } sprite)
            return sprite;

        foreach (var suffix in FilledSuffixes)
        {
            if (id.EndsWith(suffix, StringComparison.Ordinal) && id.Length > suffix.Length)
            {
                if (ResolveSprite(id[..^suffix.Length], depth: 0) is { } baseSprite)
                    return baseSprite;
            }
        }

        return null;
    }

    private string? ResolveSprite(string id, int depth)
    {
        // Inheritance in these files is shallow; a limit keeps a cycle from hanging the editor.
        if (depth > 10 || !_entities.TryGetValue(id, out var entity))
            return null;

        if (entity.Sprite != null)
            return entity.Sprite;

        foreach (var parent in entity.Parents)
        {
            if (ResolveSprite(parent, depth + 1) is { } sprite)
                return sprite;
        }

        return null;
    }

    /// <summary>
    /// Pulls entity prototypes and their sprites out of a prototype file.
    /// </summary>
    public static List<ClothingEntity> ParseEntities(string yaml)
    {
        var entities = new List<ClothingEntity>();

        YamlStream stream;
        try
        {
            stream = new YamlStream();
            stream.Load(new StringReader(yaml));
        }
        catch (Exception e)
        {
            Log.Debug(e, "Skipping unparseable entity prototype file");
            return entities;
        }

        foreach (var document in stream.Documents)
        {
            if (document.RootNode is not YamlSequenceNode root)
                continue;

            foreach (var node in root.Children.OfType<YamlMappingNode>())
            {
                if (Scalar(node, "type") != "entity" || Scalar(node, "id") is not { } id)
                    continue;

                entities.Add(new ClothingEntity(id, ParseParents(node), ParseSprite(node)));
            }
        }

        return entities;
    }

    /// <summary>
    /// A prototype's <c>parent</c>, which is either one name or a list of them.
    /// </summary>
    private static List<string> ParseParents(YamlMappingNode node)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode("parent"), out var parent))
            return [];

        return parent switch
        {
            YamlScalarNode { Value: { } single } => [single],
            YamlSequenceNode sequence => sequence.Children
                .OfType<YamlScalarNode>()
                .Select(child => child.Value)
                .Where(value => value != null)
                .ToList()!,
            _ => [],
        };
    }

    /// <summary>
    /// The sprite folder, preferring the one the Clothing component names: that is the sprite used
    /// when the item is worn, which is the only one we care about.
    /// </summary>
    private static string? ParseSprite(YamlMappingNode node)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode("components"), out var components)
            || components is not YamlSequenceNode list)
        {
            return null;
        }

        string? spriteComponent = null;

        foreach (var component in list.OfType<YamlMappingNode>())
        {
            var type = Scalar(component, "type");
            var sprite = Scalar(component, "sprite");

            if (sprite == null)
                continue;

            if (type == "Clothing")
                return sprite;

            if (type == "Sprite")
                spriteComponent = sprite;
        }

        return spriteComponent;
    }

    private static string? Scalar(YamlMappingNode node, string key)
    {
        return node.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlScalarNode scalar
            ? scalar.Value
            : null;
    }
}
