using System.Collections.Immutable;

namespace SS14.Launcher.Models.Character;

/// <summary>
/// A content repository the character editor can pull sprites and prototypes from.
/// </summary>
/// <param name="Id">Stable id, also the name of the cache folder.</param>
/// <param name="DisplayName">Shown in the fork drop-down.</param>
/// <param name="ProfileForkId">
///     What this fork calls itself in a saved character file's <c>forkId</c>.
/// </param>
public sealed record CharacterFork(
    string Id,
    string DisplayName,
    string Owner,
    string Repo,
    string Branch,
    string ProfileForkId)
{
    public string RawUrl(string path) => $"https://raw.githubusercontent.com/{Owner}/{Repo}/{Branch}/{path}";

    /// <summary>
    /// The same file through a CDN, for when raw.githubusercontent is unhappy.
    /// </summary>
    public string CdnUrl(string path) => $"https://cdn.jsdelivr.net/gh/{Owner}/{Repo}@{Branch}/{path}";

    public string TreeUrl => $"https://api.github.com/repos/{Owner}/{Repo}/git/trees/{Branch}?recursive=1";

    /// <summary>
    /// The forks offered in the editor.
    /// </summary>
    public static readonly ImmutableArray<CharacterFork> All =
    [
        new("wizden", "Space Wizards", "space-wizards", "space-station-14", "master", "space_station_14"),
        new("deadspace", "Dead Space 14", "dead-space-server", "dead-space-14", "master", "dsfobos"),
        new("lust", "Lust Station", "makura-games", "lust-station", "master", "lust_station_stable"),
    ];
}
