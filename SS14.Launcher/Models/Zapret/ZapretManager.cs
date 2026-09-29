using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace SS14.Launcher.Models.Zapret;

/// <summary>
/// Sets up and runs zapret, the DPI circumvention tool, for players whose provider throttles the
/// game's hubs and content CDNs.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is bundled with the launcher: the release archive is fetched from the project's own
/// GitHub releases when the player asks for it, and unpacked into the launcher's data folder so a
/// launcher update does not wipe it. The launcher only writes one file inside it — the user host
/// list the scripts already load — and otherwise leaves the installation alone.
/// </para>
/// <para>
/// This needs administrator rights, because zapret filters packets through a kernel driver
/// (WinDivert). That is also why antivirus software often reacts to it. Both facts are the
/// player's to accept, so every step here happens only on an explicit button press.
/// </para>
/// </remarks>
public sealed class ZapretManager(HttpClient http)
{
    /// <summary>
    /// The project the archive comes from. Nothing else is accepted as a download source.
    /// </summary>
    public const string Repository = "Flowseal/zapret-discord-youtube";

    public const string ProjectUrl = "https://github.com/" + Repository;

    private const string LatestReleaseUrl = "https://api.github.com/repos/" + Repository + "/releases/latest";

    /// <summary>
    /// Hosts the release archive may be served from. GitHub redirects downloads to its object
    /// storage, and a release asset pointing anywhere else is not something we run.
    /// </summary>
    private static readonly string[] AllowedDownloadHosts =
    [
        "github.com",
        "api.github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
    ];

    /// <summary>
    /// The copy that ships with the launcher, sitting next to the executable.
    /// </summary>
    /// <remarks>
    /// Shipped rather than downloaded on demand, because the connections that need this are the
    /// same ones that cannot reach GitHub to fetch it. Players who do not want it delete it on
    /// the first run.
    /// </remarks>
    public static string? BundledDir
    {
        get
        {
            // The launcher runs out of bin_x64, so the bundle sits one level up, beside the
            // executable the player actually clicks.
            foreach (var candidate in new[]
                     {
                         Path.Combine(LauncherPaths.DirLauncherInstall, "zapret"),
                         Path.Combine(LauncherPaths.DirLauncherInstall, "..", "zapret"),
                     })
            {
                if (File.Exists(Path.Combine(candidate, "bin", "winws.exe")))
                    return Path.GetFullPath(candidate);
            }

            return null;
        }
    }

    /// <summary>
    /// Where a downloaded copy goes: with the launcher's data, so a launcher update keeps it.
    /// </summary>
    public static string DownloadedDir => Path.Combine(LauncherPaths.DirUserData, "zapret");

    /// <summary>
    /// The copy in use: the one that shipped with the launcher, or a downloaded one.
    /// </summary>
    public static string InstallDir => BundledDir ?? DownloadedDir;

    public static string WinwsPath => Path.Combine(InstallDir, "bin", "winws.exe");

    /// <summary>
    /// The user host list the shipped scripts load beside their own.
    /// </summary>
    public static string UserListPath => Path.Combine(InstallDir, "lists", "list-general-user.txt");

    public static bool IsInstalled => File.Exists(WinwsPath);

    /// <summary>
    /// True while zapret is filtering traffic.
    /// </summary>
    public static bool IsRunning => Process.GetProcessesByName("winws").Length > 0;

    /// <summary>
    /// The strategies the project ships, as picked in the options. Each is one .bat in the
    /// installation; which one works depends on the provider, so the player has to try a few.
    /// </summary>
    public static IReadOnlyList<string> Strategies()
    {
        if (!Directory.Exists(InstallDir))
            return [];

        try
        {
            return Directory.GetFiles(InstallDir, "general*.bat")
                .Select(Path.GetFileName)
                .Where(name => name != null)
                .OrderBy(name => name!.Length)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray()!;
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not list zapret strategies");

            return [];
        }
    }

    /// <summary>
    /// Downloads the latest release and unpacks it.
    /// </summary>
    /// <param name="progress">Reports what is happening, for the status line.</param>
    /// <returns>Null when it worked, otherwise a short reason to show the player.</returns>
    public async Task<string?> InstallAsync(IProgress<string>? progress, CancellationToken cancel = default)
    {
        try
        {
            progress?.Report("fetching");

            var release = await http.GetFromJsonAsync<GitHubRelease>(LatestReleaseUrl, cancel);
            var asset = release?.Assets?.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            if (asset == null)
            {
                Log.Warning("The zapret release has no zip asset");

                return "no zip in the latest release";
            }

            if (!Uri.TryCreate(asset.Url, UriKind.Absolute, out var url)
                || url.Scheme != Uri.UriSchemeHttps
                || !AllowedDownloadHosts.Contains(url.Host, StringComparer.OrdinalIgnoreCase))
            {
                Log.Warning("Refusing to download zapret from {Url}", asset.Url);

                return "the release points somewhere unexpected";
            }

            Log.Information("Downloading zapret {Version} from {Url}", release!.TagName, url);
            progress?.Report("downloading");

            var archive = Path.Combine(Path.GetTempPath(), $"zapret-{release.TagName}.zip");

            await using (var file = File.Create(archive))
            {
                await http.DownloadToStream(url.ToString(), file, cancel: cancel);
            }

            progress?.Report("unpacking");

            // A half-unpacked older copy would be worse than none, so it goes first.
            if (Directory.Exists(DownloadedDir))
                Directory.Delete(DownloadedDir, recursive: true);

            Directory.CreateDirectory(DownloadedDir);
            ExtractFlattened(archive, DownloadedDir);
            File.Delete(archive);

            if (!IsInstalled)
            {
                Log.Warning("The zapret archive did not contain {Winws}", WinwsPath);

                return "the archive is not what we expected";
            }

            WriteHostList();

            Log.Information("Installed zapret {Version} into {Dir}", release.TagName, InstallDir);

            return null;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to install zapret");

            // The reason matters here: this download is exactly the thing a provider is likely to
            // be blocking, and "try the archive by hand" is only obvious advice once you know that.
            return e.GetBaseException().Message;
        }
    }

    /// <summary>
    /// Sets up from an archive the player downloaded themselves.
    /// </summary>
    /// <remarks>
    /// The whole point of this tool is that some connections cannot reach what they need, and
    /// GitHub is often among the casualties. When the download fails, fetching the archive any
    /// other way and pointing the launcher at it has to keep working.
    /// </remarks>
    public static string? InstallFromArchive(string archivePath)
    {
        try
        {
            if (Directory.Exists(DownloadedDir))
                Directory.Delete(DownloadedDir, recursive: true);

            Directory.CreateDirectory(DownloadedDir);
            ExtractFlattened(archivePath, DownloadedDir);

            if (!IsInstalled)
                return "the archive is not what we expected";

            WriteHostList();

            Log.Information("Installed zapret from {Archive}", archivePath);

            return null;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to install zapret from {Archive}", archivePath);

            return e.GetBaseException().Message;
        }
    }

    /// <summary>
    /// Unpacks the archive, dropping the single wrapper folder the release is packed in.
    /// </summary>
    internal static void ExtractFlattened(string archivePath, string target)
    {
        using var archive = ZipFile.OpenRead(archivePath);

        var prefix = CommonPrefix(archive.Entries.Select(entry => entry.FullName));

        foreach (var entry in archive.Entries)
        {
            var relative = entry.FullName[prefix.Length..].TrimStart('/');
            if (relative.Length == 0)
                continue;

            var destination = Path.GetFullPath(Path.Combine(target, relative));

            // A zip entry must never write outside the folder we unpack into.
            if (!destination.StartsWith(Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning("Skipping zapret archive entry {Entry}: it points outside the install folder", entry.FullName);
                continue;
            }

            if (entry.Name.Length == 0)
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }
    }

    /// <summary>
    /// The folder every entry sits in, or "" when the archive has no single root.
    /// </summary>
    internal static string CommonPrefix(IEnumerable<string> entries)
    {
        string? prefix = null;

        foreach (var entry in entries)
        {
            var slash = entry.IndexOf('/');
            var root = slash < 0 ? "" : entry[..(slash + 1)];

            if (root.Length == 0)
                return "";

            prefix ??= root;

            if (prefix != root)
                return "";
        }

        return prefix ?? "";
    }

    /// <summary>
    /// Deletes the installation, for players who do not need it.
    /// </summary>
    /// <returns>Null when it is gone, otherwise why it could not be removed.</returns>
    public static string? Uninstall()
    {
        var dir = InstallDir;

        try
        {
            if (IsRunning)
                Stop();

            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);

            Log.Information("Removed zapret from {Dir}", dir);

            return null;
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not remove zapret from {Dir}", dir);

            return e.GetBaseException().Message;
        }
    }

    /// <summary>
    /// Writes the game's hosts into the user list, replacing whatever we wrote last time.
    /// </summary>
    public static void WriteHostList()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(UserListPath)!);
            File.WriteAllText(UserListPath, ZapretHosts.FileContents());
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not write the zapret host list");
        }
    }

    /// <summary>
    /// Starts a strategy. Asks Windows for administrator rights, which is a prompt the player sees.
    /// </summary>
    public static bool Start(string strategy)
    {
        var script = Path.Combine(InstallDir, strategy);

        if (!File.Exists(script))
        {
            Log.Warning("No such zapret strategy: {Script}", script);

            return false;
        }

        WriteHostList();

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = script,
                WorkingDirectory = InstallDir,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Minimized,
            });

            return true;
        }
        catch (Exception e)
        {
            // Declining the administrator prompt lands here, which is a normal thing to do.
            Log.Information(e, "Did not start zapret");

            return false;
        }
    }

    /// <summary>
    /// Stops it by ending the filtering process, which also unloads the driver.
    /// </summary>
    public static bool Stop()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = "/F /IM winws.exe",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            });

            return true;
        }
        catch (Exception e)
        {
            Log.Information(e, "Did not stop zapret");

            return false;
        }
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("assets")] GitHubAsset[]? Assets);

    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string Url);
}
