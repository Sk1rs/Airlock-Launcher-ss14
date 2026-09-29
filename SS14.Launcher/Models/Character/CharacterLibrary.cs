using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;

namespace SS14.Launcher.Models.Character;

/// <summary>
/// A character file found on disk, wherever it came from.
/// </summary>
/// <param name="Path">The file itself.</param>
/// <param name="Source">The data folder it lives under, e.g. the name of the launcher that wrote it.</param>
/// <param name="Profile">What the file says.</param>
public sealed record LibraryEntry(string Path, string Source, CharacterProfile Profile)
{
    /// <summary>
    /// True when the file belongs to this launcher's game data, so the game sees it when started
    /// from here; characters from other launchers are shown too, but only as a source to load from.
    /// </summary>
    public bool IsOurs => System.IO.Path.GetFullPath(System.IO.Path.GetDirectoryName(Path)!)
        .Equals(System.IO.Path.GetFullPath(CharacterLibrary.OurFolder), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Finds every character file on this machine.
/// </summary>
/// <remarks>
/// Every SS14 launcher keeps the game's data under its own folder in the roaming profile, and
/// the game writes characters to <c>data/CHARACTERS</c> beneath that. Scanning the roaming
/// profile for that layout picks up characters from all of them, so a player who has used the
/// official launcher for years sees their whole cast here without copying anything by hand.
/// </remarks>
public static class CharacterLibrary
{
    public static string OurFolder => LauncherDataFolders.Own(CharacterProfile.CharactersFolder);

    /// <summary>
    /// Folders that may hold character files, ours first.
    /// </summary>
    public static IEnumerable<string> Folders() => LauncherDataFolders.All(CharacterProfile.CharactersFolder);

    /// <summary>
    /// Reads every character file it can find. Files that are not characters are skipped.
    /// </summary>
    public static List<LibraryEntry> Scan()
    {
        var entries = new List<LibraryEntry>();

        foreach (var folder in Folders())
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(folder, "*.yml");
            }
            catch (Exception e)
            {
                Log.Debug(e, "Could not list characters in {Folder}", folder);
                continue;
            }

            var source = LauncherDataFolders.SourceName(folder);

            foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (CharacterProfile.Parse(File.ReadAllText(file)) is { } profile)
                        entries.Add(new LibraryEntry(file, source, profile));
                }
                catch (Exception e)
                {
                    Log.Debug(e, "Skipping unreadable character file {File}", file);
                }
            }
        }

        return entries;
    }
}
