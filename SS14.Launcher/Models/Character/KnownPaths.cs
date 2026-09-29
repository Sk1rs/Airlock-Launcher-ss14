using System.Collections.Immutable;

namespace SS14.Launcher.Models.Character;

/// <summary>
/// Paths to try when GitHub will not hand over a repository's file list.
/// </summary>
/// <remarks>
/// Listing a repository costs one call against an hourly budget that is shared by everyone behind
/// the same address, so it can simply be unavailable. Fetching individual files is not limited that
/// way, which makes guessing at well-known paths a workable fallback: every fork in this family
/// inherits the upstream layout, so this finds the standard species and markings even when the
/// listing is refused. Fork-specific additions only show up once the listing works.
/// </remarks>
public static class KnownPaths
{
    /// <summary>
    /// Species sprite folders that exist upstream or in a fork we know of.
    /// </summary>
    public static readonly ImmutableArray<string> SpeciesFolders =
    [
        "Human",
        "Arachnid",
        "Diona",
        "Dwarf",
        "Felinid",
        "Gingerbread",
        "Harpy",
        "Moth",
        "Oni",
        "Reptilian",
        "Rodentia",
        "Shadowkin",
        "Skeleton",
        "Slime",
        "Tajaran",
        "Unathi",
        "Vox",
        "Vulpkanin",
    ];

    /// <summary>
    /// Species prototype files, for the skin colouration setting.
    /// </summary>
    public static readonly ImmutableArray<string> SpeciesPrototypes =
    [
        "Species/human.yml",
        "Species/arachnid.yml",
        "Species/diona.yml",
        "Species/dwarf.yml",
        "Species/gingerbread.yml",
        "Species/moth.yml",
        "Species/reptilian.yml",
        "Species/skeleton.yml",
        "Species/slime.yml",
        "Species/vox.yml",
        "Species/vulpkanin.yml",
    ];

    /// <summary>
    /// Name files, relative to <c>Resources/Locale/&lt;culture&gt;/</c>. These are what turn prototype
    /// ids into the words the fork itself uses.
    /// </summary>
    public static readonly ImmutableArray<string> LocaleFiles =
    [
        "species/species.ftl",
        "job/job-names.ftl",
        "markings/arachnid.ftl",
        "markings/cat.ftl",
        "markings/diona.ftl",
        "markings/ears.ftl",
        "markings/gauze.ftl",
        "markings/moth.ftl",
        "markings/noses.ftl",
        "markings/reptilian.ftl",
        "markings/scars.ftl",
        "markings/slimeperson.ftl",
        "markings/tattoos.ftl",
        "markings/undergarment.ftl",
        "markings/vox.ftl",
        "markings/vulpkanin.ftl",
        "markings/hair.ftl",
        "markings/markings.ftl",
    ];

    /// <summary>
    /// Marking prototype files, relative to <c>Resources/Prototypes/</c>.
    /// </summary>
    public static readonly ImmutableArray<string> MarkingPrototypes =
    [
        "Entities/Mobs/Customization/Markings/arachnid.yml",
        "Entities/Mobs/Customization/Markings/cat_parts.yml",
        "Entities/Mobs/Customization/Markings/diona.yml",
        "Entities/Mobs/Customization/Markings/ears.yml",
        "Entities/Mobs/Customization/Markings/gauze.yml",
        "Entities/Mobs/Customization/Markings/human_facial_hair.yml",
        "Entities/Mobs/Customization/Markings/human_hair.yml",
        "Entities/Mobs/Customization/Markings/human_noses.yml",
        "Entities/Mobs/Customization/Markings/moth.yml",
        "Entities/Mobs/Customization/Markings/reptilian.yml",
        "Entities/Mobs/Customization/Markings/scars.yml",
        "Entities/Mobs/Customization/Markings/slime.yml",
        "Entities/Mobs/Customization/Markings/tattoos.yml",
        "Entities/Mobs/Customization/Markings/undergarments.yml",
        "Entities/Mobs/Customization/Markings/vox_facial_hair.yml",
        "Entities/Mobs/Customization/Markings/vox_hair.yml",
        "Entities/Mobs/Customization/Markings/vox_parts.yml",
        "Entities/Mobs/Customization/Markings/vox_scars.yml",
        "Entities/Mobs/Customization/Markings/vox_tattoos.yml",
        "Entities/Mobs/Customization/Markings/Vulpkanin/vulpkanin_chest.yml",
        "Entities/Mobs/Customization/Markings/Vulpkanin/vulpkanin_ears.yml",
        "Entities/Mobs/Customization/Markings/Vulpkanin/vulpkanin_hair.yml",
        "Entities/Mobs/Customization/Markings/Vulpkanin/vulpkanin_head.yml",
        "Entities/Mobs/Customization/Markings/Vulpkanin/vulpkanin_limbs.yml",
        "Entities/Mobs/Customization/Markings/Vulpkanin/vulpkanin_snout.yml",
        "Entities/Mobs/Customization/Markings/Vulpkanin/vulpkanin_tail.yml",
    ];
}
