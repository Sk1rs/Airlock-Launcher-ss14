using System;
using System.Collections.Generic;
using System.IO;
using Serilog;

namespace SS14.Launcher.Models;

/// <summary>
/// Finds a game data folder across every SS14 launcher installed for this user.
/// </summary>
/// <remarks>
/// Each launcher keeps the game's data under its own folder in the roaming profile, and the game
/// writes characters, screenshots and exports into the same layout beneath it. A player who used
/// the official launcher for a year has all of that there, so anything that shows such files looks
/// in every one of them rather than only in ours.
/// </remarks>
public static class LauncherDataFolders
{
    /// <summary>
    /// Our own copy of a data subfolder, created if it is not there yet.
    /// </summary>
    public static string Own(string subfolder)
    {
        var path = Path.Combine(LauncherPaths.DirClientData, subfolder);
        Directory.CreateDirectory(path);

        return path;
    }

    /// <summary>
    /// Every folder by that name, ours first, skipping ones that do not exist.
    /// </summary>
    public static IEnumerable<string> All(string subfolder)
    {
        var own = Own(subfolder);
        yield return own;

        var roaming = Directory.GetParent(LauncherPaths.DirDataRoot)?.FullName;
        if (roaming == null || !Directory.Exists(roaming))
            yield break;

        IEnumerable<string> siblings;
        try
        {
            siblings = Directory.EnumerateDirectories(roaming);
        }
        catch (Exception e)
        {
            Log.Debug(e, "Could not list {Roaming} for other launchers", roaming);
            yield break;
        }

        foreach (var sibling in siblings)
        {
            var folder = Path.Combine(sibling, "data", subfolder);

            if (Directory.Exists(folder) && !folder.Equals(own, StringComparison.OrdinalIgnoreCase))
                yield return folder;
        }
    }

    /// <summary>
    /// Which launcher a folder belongs to, for labelling where a file came from.
    /// </summary>
    public static string SourceName(string folder)
    {
        // <launcher name>/data/<subfolder>, so the name is two levels up.
        return Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(folder)!)!) ?? folder;
    }
}
