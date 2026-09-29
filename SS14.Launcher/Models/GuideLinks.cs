using System.Collections.Generic;

namespace SS14.Launcher.Models;

/// <summary>
/// One place to read up on, with the language it is written in.
/// </summary>
/// <param name="TitleKey">Localization key for the caption.</param>
/// <param name="Language">Two-letter tag shown next to it, so a player picks the one they can read.</param>
/// <param name="Url">Where it opens.</param>
public sealed record GuideLink(string TitleKey, string Language, string Url);

/// <summary>
/// A heading and the links under it.
/// </summary>
public sealed record GuideSection(string TitleKey, IReadOnlyList<GuideLink> Links);

/// <summary>
/// Where new players get sent for wikis and role guides.
/// </summary>
/// <remarks>
/// Hand-picked rather than scraped: these are the pages the community actually uses, in the two
/// languages our players read. Community guides move around, so anything that goes dead is a
/// one-line change here.
/// </remarks>
public static class GuideLinks
{
    public static readonly IReadOnlyList<GuideSection> Sections =
    [
        new GuideSection("guides-section-wiki",
        [
            new GuideLink("guides-wiki-corvax", "RU", "https://station14.ru"),
            new GuideLink("guides-wiki-fandom", "EN", "https://space-station-14.fandom.com/wiki/Space_Station_14_Wiki"),
        ]),
        new GuideSection("guides-section-roles",
        [
            new GuideLink("guides-role-chemistry", "RU", "https://steamcommunity.com/sharedfiles/filedetails/?id=2739538889"),
            new GuideLink("guides-role-chemistry", "EN", "https://docs.google.com/spreadsheets/d/1-U_emy1uNRW--982WoYZGpprhrN4A2v7VEfik_6Zshw/htmlview#gid=824532071"),
            new GuideLink("guides-role-bartender", "RU", "https://steamcommunity.com/sharedfiles/filedetails/?l=russian&id=2949639304"),
            new GuideLink("guides-role-bartender", "EN", "https://docs.google.com/document/d/12FynP4f06s6Vo1Qd8PRuR7DbIkFu12jlv6K3bTTiIx0/edit?tab=t.0#heading=h.kn6wtsw6upeo"),
        ]),
    ];

    /// <summary>
    /// Where the game drops screenshots when started from this launcher.
    /// </summary>
    public const string ScreenshotsFolder = "Screenshots";
}
