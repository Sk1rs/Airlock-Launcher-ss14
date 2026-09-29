using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Serilog;

namespace SS14.Launcher;

/// <summary>
///     Contains file paths used by the launcher to manage data.
/// </summary>
public static class LauncherPaths
{
    public static readonly string AppDataPath = Path.Combine(GetDataDirName(), GetAppDataName());
    public static readonly string EngineInstallationsDirName = "engines";
    public static readonly string EngineModulesDirName = "modules";
    public static readonly string ServerContentDirName = "server content";
    public static readonly string LogsDirName = "logs";
    public static readonly string LauncherLogName = "launcher-.log"; // Serilog will append yyyyMMdd to the filename
    public static readonly string ClientMacLogName = "client.mac.log";
    public static readonly string ClientStdoutLogName = "client.stdout.log";
    public static readonly string ClientStderrLogName = "client.stderr.log";

    public static readonly string DirLauncherInstall = GetInstallDir();
    /// <summary>
    ///     Root folder shared by the launcher and the game client, e.g. <c>%APPDATA%/Airlock Launcher</c>.
    /// </summary>
    public static readonly string DirDataRoot = Path.Combine(GetAppDataBase(), GetDataDirName());
    /// <summary>
    ///     Where the game client keeps its own data: characters, screenshots, exported sprites and so on.
    ///     The client picks this itself from <c>SS14_LAUNCHER_DATADIR</c>, we just mirror the layout here.
    /// </summary>
    public static readonly string DirClientData = Path.Combine(DirDataRoot, "data");
    /// <summary>
    ///     Where the game client writes exported character PNGs.
    /// </summary>
    public static readonly string DirClientExports = Path.Combine(DirClientData, "Exports");
    public static readonly string DirUserData = GetUserDataDir();
    public static readonly string DirLocalData = GetLocalUserDataDir();
    public static readonly string DirEngineInstallations = Path.Combine(DirUserData, EngineInstallationsDirName);
    public static readonly string DirModuleInstallations = Path.Combine(DirUserData, EngineModulesDirName);
    // Legacy server content directory. No longer used except to delete on launch.
    public static readonly string DirServerContent = Path.Combine(DirUserData, ServerContentDirName);
    public static readonly string DirLogs = Path.Combine(DirUserData, LogsDirName);
    public static readonly string PathLauncherLog = Path.Combine(DirLogs, LauncherLogName);
    public static readonly string PathClientMacLog = Path.Combine(DirLogs, ClientMacLogName);
    public static readonly string PathClientStdoutLog = Path.Combine(DirLogs, ClientStdoutLogName);
    public static readonly string PathClientStderrLog = Path.Combine(DirLogs, ClientStderrLogName);
    public static readonly string PathPublicKey = Path.Combine(DirLauncherInstall, "signing_key_Robust"); // Used as a fallback
    public static readonly Dictionary<string, string> PathPublicKeys = new()
    {
        { "Robust", PathPublicKey },
        { "Multiverse", Path.Combine(DirLauncherInstall, "signing_key_Multiverse") },
        { "Supermatter", Path.Combine(DirLauncherInstall, "signing_key_Supermatter") },
    };
    public static readonly string PathContentDb = Path.Combine(DirLocalData, "content.db");
    public static readonly string PathOverrideAssetsDb = Path.Combine(DirLocalData, "override_assets.db");

    public static void CreateDirs()
    {
        MigrateLegacyLocalData();

        Ensure(DirLogs);
        Ensure(DirLocalData);
        Ensure(DirEngineInstallations);
        Ensure(DirModuleInstallations);

        static void Ensure(string path) => Helpers.EnsureDirectoryExists(path);
    }

    /// <summary>
    ///     Brings the local-appdata half of an older install across after a rename.
    /// </summary>
    /// <remarks>
    ///     The roaming folder holds settings and logins, but the downloaded game content lives in
    ///     local appdata as content.db, and that is several gigabytes. Moving only the roaming half
    ///     leaves a launcher that looks fine and then re-downloads the entire game, so this has to
    ///     happen too — and before anything opens the content database.
    /// </remarks>
    private static void MigrateLegacyLocalData()
    {
        if (Environment.GetEnvironmentVariable("SS14_LAUNCHER_DATADIR") != null)
            return;

        var appDataBase = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : GetAppDataBase();

        var target = Path.Combine(appDataBase, DefaultDataDirName);

        foreach (var legacy in LegacyDataDirNames)
        {
            var source = Path.Combine(appDataBase, legacy);
            if (!Directory.Exists(source) || Path.GetFullPath(source) == Path.GetFullPath(target))
                continue;

            MergeFolder(source, target);
        }
    }

    /// <summary>
    ///     Moves a folder's files into another, keeping whichever copy holds more.
    /// </summary>
    /// <remarks>
    ///     Everything down here is a cache or a content database: regenerable, and bigger means
    ///     "has more downloaded in it". So a file missing on the other side is moved, a file that
    ///     exists on both is replaced only when the old one is larger — which is exactly the case
    ///     of a freshly created, empty content.db sitting where four gigabytes should be. Files
    ///     that cannot be moved are left where they are; the next start tries again.
    /// </remarks>
    internal static void MergeFolder(string source, string target)
    {
        try
        {
            Directory.CreateDirectory(target);

            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(target, Path.GetRelativePath(source, file));

                try
                {
                    if (File.Exists(destination) && new FileInfo(destination).Length >= new FileInfo(file).Length)
                        continue;

                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Move(file, destination, overwrite: true);
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Could not move {File} to {Destination}", file, destination);
                }
            }

            // Only removed once it has nothing left worth keeping.
            if (!Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).Any())
                Directory.Delete(source, recursive: true);

            Log.Information("Merged old data folder {Source} into {Target}", source, target);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not merge {Source} into {Target}", source, target);
        }
    }

    private static string GetInstallDir()
    {
        return Path.GetDirectoryName(typeof(LauncherPaths).Assembly.Location)!;
    }

    private static string GetUserDataDir()
    {
        return Path.Combine(GetAppDataBase(), AppDataPath);
    }

    /// <summary>
    ///     Platform-specific directory that <see cref="DirDataRoot"/> lives in.
    /// </summary>
    private static string GetAppDataBase()
    {
        string appDataDir;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (xdgDataHome == null)
            {
                appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            }
            else
            {
                appDataDir = xdgDataHome;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support");
        }
        else
        {
            appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        return appDataDir;
    }

    private static string GetLocalUserDataDir()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, AppDataPath);
        }

        return GetUserDataDir();
    }

    public static string GetAppDataName()
        => Environment.GetEnvironmentVariable("SS14_LAUNCHER_APPDATA_NAME") ?? "launcher";

    /// <summary>
    ///     Name of the root folder all launcher and client data is stored under.
    /// </summary>
    /// <remarks>
    ///     Deliberately different from the upstream/Steam builds so this launcher never shares
    ///     its data directory (and thus its databases and configs) with another install.
    ///     Can be overridden with the <c>SS14_LAUNCHER_DATADIR</c> environment variable.
    /// </remarks>
    public static string GetDataDirName() => _dataDirName ??= ResolveDataDirName();

    public const string DefaultDataDirName = "Airlock Launcher";

    /// <summary>
    ///     Folder names this launcher used before it was renamed, newest first.
    /// </summary>
    /// <remarks>
    ///     A property, not a field: the paths above run their initializers before any field down
    ///     here has a value, and they resolve the directory name while doing so.
    /// </remarks>
    private static string[] LegacyDataDirNames => ["4appa luncher"];

    /// <summary>
    ///     The same names, for the recovery that reads an older folder rather than moving it.
    /// </summary>
    public static IReadOnlyList<string> LegacyDataDirNamesPublic => LegacyDataDirNames;

    /// <summary>
    ///     Resolved once: the folder is moved at most one time per install, and everything else
    ///     hangs off this name.
    /// </summary>
    /// <remarks>
    ///     Cached in a plain field rather than a <see cref="Lazy{T}"/> field: static fields are
    ///     initialized in declaration order, and the paths above ask for this name while building
    ///     themselves, which is before any field declared down here would have a value.
    /// </remarks>
    private static string? _dataDirName;

    /// <summary>
    ///     Picks the data folder, carrying an older one over if this install has one.
    /// </summary>
    /// <remarks>
    ///     The folder holds the settings database, logins, characters and several gigabytes of
    ///     downloaded game content, so a rename must bring it along rather than start empty. The
    ///     move is a rename within one drive, so it costs nothing. If it fails — most likely the
    ///     game still running out of that folder — the old name keeps being used, which is far
    ///     better than silently losing the lot; the next start tries again.
    /// </remarks>
    private static string ResolveDataDirName()
    {
        if (Environment.GetEnvironmentVariable("SS14_LAUNCHER_DATADIR") is { } overridden)
            return overridden;

        var baseDir = GetAppDataBase();
        var target = Path.Combine(baseDir, DefaultDataDirName);

        // An existing folder is only the right one if the settings are actually in it. A folder
        // that exists but has no settings means something created it early — a crashed run, a
        // half-finished move — and the real data is still sitting under the old name.
        if (Directory.Exists(target) && HasSettings(target))
            return DefaultDataDirName;

        foreach (var legacy in LegacyDataDirNames)
        {
            var source = Path.Combine(baseDir, legacy);
            if (!Directory.Exists(source) || !HasSettings(source))
                continue;

            try
            {
                if (!Directory.Exists(target))
                {
                    Directory.Move(source, target);
                    Log.Information("Moved data directory {Source} to {Target}", source, target);
                }
                else
                {
                    MergeFolder(source, target);
                }
            }
            catch (Exception e)
            {
                Log.Warning(e, "Could not bring {Source} over to {Target}, using it where it is", source, target);

                return legacy;
            }

            break;
        }

        return DefaultDataDirName;
    }

    /// <summary>
    ///     Whether a data folder holds the settings database, which is what makes it the real one.
    /// </summary>
    private static bool HasSettings(string dataDir)
    {
        return File.Exists(Path.Combine(dataDir, GetAppDataName(), "settings.db"));
    }
}
