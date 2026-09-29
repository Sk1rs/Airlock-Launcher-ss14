using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dapper;
using Microsoft.Data.Sqlite;
using Serilog;

namespace SS14.Launcher.Models.Data;

/// <summary>
/// Pulls data over from another SS14 launcher installed on this machine.
/// </summary>
/// <remarks>
/// Every launcher in the family keeps the same layout: <c>%APPDATA%/&lt;name&gt;/launcher/settings.db</c>
/// for favourites and settings, and <c>%LOCALAPPDATA%/&lt;name&gt;/launcher/content.db</c> for downloaded
/// game files. The settings schemas drifted apart between forks, so favourites are read column by
/// column rather than copied wholesale; the content database schema is still identical everywhere,
/// which is what makes reusing a multi-gigabyte download possible.
/// </remarks>
public static class LauncherImport
{
    /// <summary>
    /// Data directories we can name. Anything else found is still offered, just without a nice name.
    /// </summary>
    private static readonly Dictionary<string, string> KnownLaunchers = new()
    {
        ["Space Station 14"] = "Space Wizards",
        ["SimpleStation14"] = "Space Station: Beyond",
        ["SimpleStation14 Launcher"] = "Space Station: Beyond",
        ["4appa luncher"] = "Airlock Launcher (old name)",
    };

    private const string LauncherSubDir = "launcher";

    /// <summary>
    /// Another launcher's data we could import from.
    /// </summary>
    public sealed record Source(
        string DisplayName,
        string DataDirName,
        string SettingsDb,
        string? ContentDb,
        string LogDirectory,
        int FavoriteCount,
        int HubCount,
        int SessionCount,
        long ContentBytes)
    {
        public bool HasContent => ContentDb != null && ContentBytes > 0;

        public bool HasSessions => SessionCount > 0;
    }

    public sealed record Result(
        int Favorites,
        int Hubs,
        int Sessions,
        string? StatsError,
        bool Content,
        string? ContentError);

    /// <summary>
    /// Finds other launchers' data directories, ignoring this build's own.
    /// </summary>
    public static List<Source> Detect()
    {
        var sources = new List<Source>();

        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!Directory.Exists(roaming))
            return sources;

        var ourDir = LauncherPaths.GetDataDirName();

        foreach (var dir in Directory.EnumerateDirectories(roaming))
        {
            var name = Path.GetFileName(dir);
            if (string.Equals(name, ourDir, StringComparison.OrdinalIgnoreCase))
                continue;

            var settingsDb = Path.Combine(dir, LauncherSubDir, "settings.db");
            if (!File.Exists(settingsDb))
                continue;

            try
            {
                sources.Add(Describe(name, settingsDb));
            }
            catch (Exception e)
            {
                Log.Warning(e, "Failed to inspect possible launcher install at {Path}", settingsDb);
            }
        }

        return sources.OrderByDescending(s => s.ContentBytes).ThenBy(s => s.DisplayName).ToList();
    }

    /// <summary>
    /// Brings favourites and hubs over from this launcher's own older data folder, when it has
    /// them and we have none.
    /// </summary>
    /// <remarks>
    /// The rename moves the data folder, but a run that started before the move — or one that
    /// created the new folder first and left the old one alone — ends with the player logged in,
    /// staring at an empty favourites list while ten servers sit in a folder next door. Nobody
    /// should have to know that to get their list back, so it comes across on its own. Only when
    /// ours is empty, so it can never overwrite a list the player has since built up.
    /// </remarks>
    public static int RecoverFavoritesFromOldName(DataManager cfg)
    {
        if (cfg.FavoriteServers.Count > 0)
            return 0;

        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var ourDir = LauncherPaths.GetDataDirName();

        foreach (var legacy in LauncherPaths.LegacyDataDirNamesPublic)
        {
            if (string.Equals(legacy, ourDir, StringComparison.OrdinalIgnoreCase))
                continue;

            var settingsDb = Path.Combine(roaming, legacy, LauncherSubDir, "settings.db");
            if (!File.Exists(settingsDb))
                continue;

            try
            {
                var source = Describe(legacy, settingsDb);
                if (source.FavoriteCount == 0)
                    continue;

                using var con = OpenReadOnly(settingsDb);
                var imported = ImportFavorites(con, cfg);
                var hubs = ImportHubs(con, cfg);

                if (imported > 0 || hubs > 0)
                    cfg.CommitConfig();

                Log.Information(
                    "Recovered {Favorites} favourites and {Hubs} hubs from the old data folder {Legacy}",
                    imported,
                    hubs,
                    legacy);

                return imported;
            }
            catch (Exception e)
            {
                Log.Warning(e, "Could not recover favourites from {Path}", settingsDb);
            }
        }

        return 0;
    }

    private static Source Describe(string dataDirName, string settingsDb)
    {
        using var con = OpenReadOnly(settingsDb);

        var favorites = con.ExecuteScalar<int>("SELECT COUNT(*) FROM FavoriteServer");
        var hubs = con.ExecuteScalar<int>("SELECT COUNT(*) FROM Hub");

        var contentDb = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            dataDirName,
            LauncherSubDir,
            "content.db");

        var contentBytes = File.Exists(contentDb) ? new FileInfo(contentDb).Length : 0;

        var logDirectory = Path.Combine(Path.GetDirectoryName(settingsDb)!, "logs");
        var sessions = LauncherLogSessions.ReadFolder(logDirectory).Count;

        return new Source(
            KnownLaunchers.GetValueOrDefault(dataDirName, dataDirName),
            dataDirName,
            settingsDb,
            File.Exists(contentDb) ? contentDb : null,
            logDirectory,
            favorites,
            hubs,
            sessions,
            contentBytes);
    }

    /// <summary>
    /// Copies the selected data over. Blocking: the content database can be gigabytes.
    /// </summary>
    public static Result Import(
        Source source,
        bool favorites,
        bool hubs,
        bool stats,
        bool content,
        DataManager cfg,
        GameStatistics statistics)
    {
        var importedFavorites = 0;
        var importedHubs = 0;

        using (var con = OpenReadOnly(source.SettingsDb))
        {
            if (favorites)
                importedFavorites = ImportFavorites(con, cfg);

            if (hubs)
                importedHubs = ImportHubs(con, cfg);
        }

        if (importedFavorites > 0 || importedHubs > 0)
            cfg.CommitConfig();

        var importedSessions = 0;
        string? statsError = null;

        if (stats)
            importedSessions = ImportStatistics(source, statistics, out statsError);

        string? contentError = null;
        var importedContent = false;

        if (content)
        {
            contentError = ImportContent(source);
            importedContent = contentError == null;
        }

        return new Result(importedFavorites, importedHubs, importedSessions, statsError, importedContent, contentError);
    }

    /// <summary>
    /// Rebuilds playtime from the other launcher's logs and folds it into ours.
    /// </summary>
    private static int ImportStatistics(Source source, GameStatistics statistics, out string? error)
    {
        error = null;

        // Importing twice would count every session twice over, and the totals are all we keep,
        // so there is no way to tell the duplicates apart afterwards.
        if (!statistics.Entries.IsEmpty)
        {
            error = "not-empty";
            return 0;
        }

        var sessions = LauncherLogSessions.ReadFolder(source.LogDirectory);
        if (sessions.Count == 0)
        {
            error = "nothing";
            return 0;
        }

        var imported = 0;

        foreach (var session in sessions)
        {
            if (session.Duration < GameStatistics.MinimumSession)
                continue;

            statistics.RecordSession(session.Address, null, session.Duration);
            imported += 1;
        }

        return imported;
    }

    private static int ImportFavorites(SqliteConnection con, DataManager cfg)
    {
        var columns = con.Query<string>("SELECT name FROM pragma_table_info('FavoriteServer')")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var order = FavoriteOrderClause(columns);

        var rows = con.Query<ImportedFavorite>($"SELECT Address, Name FROM FavoriteServer ORDER BY {order}")
            .ToList();

        // Imported servers go underneath whatever is already pinned.
        var position = cfg.FavoriteServers.Items.Any()
            ? cfg.FavoriteServers.Items.Min(f => f.Position) - 1
            : -1;

        var imported = 0;

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Address) || cfg.FavoriteServers.Lookup(row.Address).HasValue)
                continue;

            cfg.AddFavoriteServer(new FavoriteServer(row.Name, row.Address, position));
            position -= 1;
            imported += 1;
        }

        return imported;
    }

    /// <summary>
    /// Picks how to order another launcher's favourites so they keep the order they were shown in.
    /// </summary>
    /// <remarks>
    /// This launcher stores an explicit <c>Position</c>; upstream still sorts by <c>RaiseTime</c>,
    /// the moment a server was last moved up. Both mean "bigger is higher up the list".
    /// </remarks>
    public static string FavoriteOrderClause(ISet<string> columns)
    {
        if (columns.Contains("Position"))
            return "Position DESC";

        if (columns.Contains("RaiseTime"))
            return "RaiseTime DESC";

        // Some ancient version we don't know: at least import them in a stable order.
        return "Address";
    }

    private static int ImportHubs(SqliteConnection con, DataManager cfg)
    {
        var rows = con.Query<ImportedHub>("SELECT Address, Priority FROM Hub ORDER BY Priority").ToList();

        var existing = cfg.Hubs.Select(h => h.Address.AbsoluteUri).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hubs = cfg.Hubs.ToList();
        var imported = 0;

        foreach (var row in rows)
        {
            if (row.Address == null || !Uri.TryCreate(row.Address, UriKind.Absolute, out var uri))
                continue;

            if (!existing.Add(uri.AbsoluteUri))
                continue;

            hubs.Add(new Hub(uri, hubs.Count));
            imported += 1;
        }

        if (imported > 0)
            cfg.SetHubs(hubs.Select((h, i) => new Hub(h.Address, i)).ToList());

        return imported;
    }

    /// <summary>
    /// Copies the other launcher's downloaded game files over, so they don't have to be downloaded again.
    /// </summary>
    /// <returns>An error message, or null on success.</returns>
    private static string? ImportContent(Source source)
    {
        if (source.ContentDb == null)
            return "no-content";

        try
        {
            // Refuse rather than merge: content ids would collide, and merging two of these is a
            // much bigger job than re-downloading whatever is already here.
            if (File.Exists(LauncherPaths.PathContentDb) && CountContentVersions(LauncherPaths.PathContentDb) > 0)
                return "not-empty";

            using var from = OpenReadOnly(source.ContentDb);
            if (!SchemasMatch(from))
                return "schema";

            Helpers.EnsureDirectoryExists(Path.GetDirectoryName(LauncherPaths.PathContentDb)!);

            using var to = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = LauncherPaths.PathContentDb,
                Mode = SqliteOpenMode.ReadWriteCreate,
            }.ToString());

            to.Open();

            // The SQLite backup API rather than a file copy: it takes the write-ahead log into
            // account, so we can't end up with a torn database.
            from.BackupDatabase(to);

            Log.Information("Imported content database from {Path}", source.ContentDb);

            return null;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to import content database from {Path}", source.ContentDb);
            return "failed";
        }
    }

    private static int CountContentVersions(string path)
    {
        try
        {
            using var con = OpenReadOnly(path);

            return con.ExecuteScalar<int>("SELECT COUNT(*) FROM ContentVersion");
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not count content versions in {Path}", path);

            // Assume it has something in it, so we don't stomp on data we failed to read.
            return 1;
        }
    }

    /// <summary>
    /// Checks the other launcher's content database went through the same migrations as ours expects.
    /// </summary>
    private static bool SchemasMatch(SqliteConnection source)
    {
        var theirs = source.Query<string>("SELECT ScriptName FROM SchemaVersions").ToHashSet();

        using var ours = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = LauncherPaths.PathContentDb,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());

        ours.Open();

        var mine = ours.Query<string>("SELECT ScriptName FROM SchemaVersions").ToHashSet();

        return theirs.SetEquals(mine);
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var con = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString());

        con.Open();

        return con;
    }

    private sealed class ImportedFavorite
    {
        public string Address { get; set; } = "";
        public string? Name { get; set; }
    }

    private sealed class ImportedHub
    {
        public string? Address { get; set; }
        public long Priority { get; set; }
    }
}
