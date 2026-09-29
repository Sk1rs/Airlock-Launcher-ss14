using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using NSec.Cryptography;
using Serilog;
using Splat;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models.EngineManager;

/// <summary>
///     Downloads engine versions from the website.
/// </summary>
public sealed partial class EngineManagerDynamic : IEngineManager
{
    public const string OverrideVersionName = "_OVERRIDE_";

    private readonly DataManager _cfg = Locator.Current.GetRequiredService<DataManager>();
    private readonly HttpClient _http = Locator.Current.GetRequiredService<HttpClient>();

    public string GetEnginePath(string engineVersion)
    {
#if DEVELOPMENT
        if (_cfg.GetCVar(CVars.EngineOverrideEnabled))
        {
            return FindOverrideZip("Robust.Client", _cfg.GetCVar(CVars.EngineOverridePath));
        }
#endif

        if (!_cfg.EngineInstallations.Lookup(engineVersion).HasValue)
        {
            throw new ArgumentException("We do not have that engine version!");
        }

        return Path.Combine(LauncherPaths.DirEngineInstallations, $"{engineVersion}.zip");
    }

    public string GetEngineModule(string moduleName, string moduleVersion)
    {
#if DEVELOPMENT
        if (_cfg.GetCVar(CVars.EngineOverrideEnabled))
            moduleVersion = OverrideVersionName;
#endif

        return Path.Combine(LauncherPaths.DirModuleInstallations, moduleName, moduleVersion);
    }

    public string GetEngineSignature(string engineVersion)
    {
#if DEVELOPMENT
        if (_cfg.GetCVar(CVars.EngineOverrideEnabled))
            return "DEADBEEF";
#endif

        return _cfg.EngineInstallations.Lookup(engineVersion).Value.Signature;
    }

    public async Task<EngineInstallationResult> DownloadEngineIfNecessary(
        string version,
        string engine,
        Helpers.DownloadProgressCallback? progress = null,
        CancellationToken cancel = default)
    {
#if DEVELOPMENT
        if (_cfg.GetCVar(CVars.EngineOverrideEnabled))
        {
            // Engine override means we don't need to download anything, we have it locally!
            // At least, if we don't, we'll just blame the developer that enabled it.
            return new EngineInstallationResult(version, false);
        }
#endif

        var foundVersion = await GetVersionInfo(version, engine, true, cancel);
        if (foundVersion == null)
        {
            // The manifest couldn't be fetched (CDN timed out / unreachable) or doesn't list this version.
            // If we already have exactly this engine installed, use it instead of refusing to connect:
            // it was verified when it was installed, and its signature is checked again before launch.
            if (_cfg.EngineInstallations.Lookup(version).HasValue)
            {
                Log.Warning("Engine manifest unavailable, using locally installed engine {Version}", version);
                return new EngineInstallationResult(version, false);
            }

            throw new UpdateException("Unable to find engine version in manifest!");
        }

        if (foundVersion.Info.Insecure)
            throw new UpdateException("Specified engine version is insecure!");

        if (version != foundVersion.Version)
            Log.Debug($"Requested engine version was {version}, redirected to {foundVersion.Version}");

        if (_cfg.EngineInstallations.Lookup(foundVersion.Version).HasValue)
        {
            // Already have the engine version, we're good.
            return new EngineInstallationResult(foundVersion.Version, false);
        }

        Log.Information("Installing engine version {version}...", foundVersion.Version);

        var bestRid = RidUtility.FindBestRid(foundVersion.Info.Platforms.Keys);
        if (bestRid == null)
        {
            throw new NoEngineForPlatformException("No engine version available for our platform!");
        }

        Log.Debug("Selecting RID {rid}", bestRid);

        var buildInfo = foundVersion.Info.Platforms[bestRid];

        Log.Debug("Downloading engine: {EngineDownloadUrl}", buildInfo.Url);

        Helpers.EnsureDirectoryExists(LauncherPaths.DirEngineInstallations);

        var downloadTarget = Path.Combine(LauncherPaths.DirEngineInstallations, $"{foundVersion.Version}.zip");
        await using var file = File.Create(downloadTarget, 4096, FileOptions.Asynchronous);

        try
        {
            await DownloadEngineFromAnyMirror(buildInfo.Url, engine, foundVersion.Version, file, progress, cancel);
        }
        catch (OperationCanceledException)
        {
            // Don't leave behind garbage.
            await file.DisposeAsync();
            File.Delete(downloadTarget);

            throw;
        }

        _cfg.AddEngineInstallation(new InstalledEngineVersion(foundVersion.Version, buildInfo.Signature));
        _cfg.CommitConfig();
        return new EngineInstallationResult(foundVersion.Version, true);
    }


    /// <summary>
    /// How long a download may go without delivering a single chunk before it is given up on.
    /// </summary>
    /// <remarks>
    /// Progress is reported every 80 KB, so this also drops any host crawling below ~5 KB/s: at
    /// that speed a 7 MB engine would take the better part of half an hour and looks frozen.
    /// </remarks>
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Downloads an engine build, switching to a mirror when the host the manifest names stalls.
    /// </summary>
    /// <remarks>
    /// The manifest points at one host for the actual zip, and from some networks that host is
    /// throttled to a crawl while the CDN serving the manifest itself has the same file and is
    /// fast. The zip is signed and checked before launch, so a mirror cannot slip in a different
    /// file; the worst a bad mirror can do is fail, and then the next one is tried. If every host
    /// stalls, the original is retried with no time limit: slow beats never.
    /// </remarks>
    private async Task DownloadEngineFromAnyMirror(
        string url,
        string engine,
        string version,
        FileStream file,
        Helpers.DownloadProgressCallback? progress,
        CancellationToken cancel)
    {
        var candidates = EngineMirrors(url, engine, version);

        foreach (var candidate in candidates)
        {
            cancel.ThrowIfCancellationRequested();

            file.SetLength(0);
            file.Position = 0;

            if (await TryDownloadWithStallGuard(_http, candidate, file, progress, cancel))
                return;

            Log.Warning("Engine download from {Url} stalled, trying the next mirror", candidate);
        }

        Log.Warning("Every engine mirror stalled; downloading {Url} however slowly it comes", url);

        file.SetLength(0);
        file.Position = 0;
        await _http.DownloadToStream(url, file, progress, cancel: cancel);
    }

    /// <summary>
    /// The manifest's own URL first, then the same path on each host that serves this engine's
    /// manifest: those hosts keep the builds alongside it.
    /// </summary>
    internal static List<string> EngineMirrors(string url, string engine, string version)
    {
        var mirrors = new List<string> { url };

        if (!Uri.TryCreate(url, UriKind.Absolute, out var original)
            || !ConfigConstants.EngineBuildsUrl.TryGetValue(engine, out var manifests))
        {
            return mirrors;
        }

        var fileName = original.Segments[^1];

        foreach (var manifestUrl in manifests.Urls)
        {
            if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var manifest))
                continue;

            var mirror = $"{manifest.GetLeftPart(UriPartial.Authority)}/builds/{version}/{fileName}";

            if (!mirrors.Contains(mirror))
                mirrors.Add(mirror);
        }

        return mirrors;
    }

    /// <summary>
    /// Runs one download, abandoning it if the data stops coming.
    /// </summary>
    /// <returns>True if the file came through; false if the host stalled or failed.</returns>
    internal static async Task<bool> TryDownloadWithStallGuard(
        HttpClient http,
        string url,
        Stream file,
        Helpers.DownloadProgressCallback? progress,
        CancellationToken cancel,
        TimeSpan? stallTimeout = null)
    {
        var timeout = stallTimeout ?? StallTimeout;

        using var guard = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        var lastData = Environment.TickCount64;
        var lastBytes = 0L;

        void Watch(long downloaded, long total)
        {
            // The first report carries the size and no data; only actual bytes reset the clock.
            if (downloaded > lastBytes)
            {
                lastBytes = downloaded;
                Volatile.Write(ref lastData, Environment.TickCount64);
            }

            progress?.Invoke(downloaded, total);
        }

        var download = http.DownloadToStream(url, file, Watch, cancel: guard.Token);

        while (true)
        {
            var finished = await Task.WhenAny(download, Task.Delay(TimeSpan.FromSeconds(1), guard.Token));

            if (finished == download)
                break;

            if (Environment.TickCount64 - Volatile.Read(ref lastData) > timeout.TotalMilliseconds)
            {
                Log.Debug("No data from {Url} for {Seconds}s after {Bytes} bytes", url, timeout.TotalSeconds, lastBytes);
                await guard.CancelAsync();
                break;
            }
        }

        try
        {
            await download;
            return true;
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            return false;
        }
        catch (HttpRequestException e)
        {
            // A mirror that does not have the file (or is down) is simply skipped.
            Log.Debug(e, "Engine mirror {Url} failed", url);
            return false;
        }
        catch (IOException e)
        {
            Log.Debug(e, "Engine mirror {Url} broke the connection", url);
            return false;
        }
    }

    public async Task<bool> DownloadModuleIfNecessary(
        string moduleName,
        string moduleVersion,
        EngineModuleManifest manifest,
        Helpers.DownloadProgressCallback? progress = null,
        CancellationToken cancel = default)
    {
#if DEVELOPMENT
        if (_cfg.GetCVar(CVars.EngineOverrideEnabled))
        {
            // For modules we have to extract them from the zip to disk first.
            // So it's a little more involved than just giving a different zip path to the launch code.
            await CopyOverrideModule(moduleName);
            return true;
        }
#endif

        // Currently the module handling code assumes all modules need straight extract to disk.
        // This works for CEF, but who knows what the future might hold?

        Log.Debug("Checking to download {ModuleName} {ModuleVersion}", moduleName, moduleVersion);

        var versionData = manifest.Modules[moduleName].Versions[moduleVersion];

        if (versionData.Insecure)
            throw new UpdateException("Selected module version is insecure!");

        Log.Debug("Selected module {ModuleName} {ModuleVersion}", moduleName, moduleVersion);

        var alreadyInstalled = _cfg.EngineModules.Any(m => m.Name == moduleName && m.Version == moduleVersion);

        if (alreadyInstalled)
        {
            Log.Debug("Already have module installed!");
            return false;
        }

        Log.Information("Installing {ModuleName} {ModuleVersion}", moduleName, moduleVersion);

        var bestRid = RidUtility.FindBestRid(versionData.Platforms.Keys);
        if (bestRid == null)
            throw new NoModuleForPlatformException("No module version available for our platform!");

        Log.Debug("Selecting RID {Rid}", bestRid);

        var platformData = versionData.Platforms[bestRid];

        Log.Debug("Downloading module: {EngineDownloadUrl}", platformData.Url);

        GetModulePaths(
            moduleName,
            moduleVersion,
            out var moduleDiskPath,
            out var moduleVersionDiskPath);

        await ClearModuleDir(moduleDiskPath, moduleVersionDiskPath);

        {
            await using var tempFile = TempFile.CreateTempFile();
            Log.Debug("Downloading into temp file: {TempFilePath}", tempFile.Name);

            await _http.DownloadToStream(platformData.Url, tempFile, progress, cancel);

            // Verify signature.
            tempFile.Seek(0, SeekOrigin.Begin);

            if (!VerifyModuleSignature(tempFile, moduleName, platformData.Sig))
            {
#if DEBUG
                if (_cfg.GetCVar(CVars.DisableSigning))
                {
                    Log.Debug("Signature check failed for module, ignoring because signing disabled");
                }
                else
#endif
                {
                    throw new UpdateException("Failed to verify module signature!");
                }
            }

            // Done downloading, extract...
            Log.Debug("Download complete, extracting into: {TempFilePath}", moduleVersionDiskPath);

            tempFile.Seek(0, SeekOrigin.Begin);

            // CEF is so horrifically huge I'm enabling disk compression on it.
            Helpers.MarkDirectoryCompress(moduleVersionDiskPath);

            ExtractModule(moduleName, moduleVersionDiskPath, tempFile);
        }

        _cfg.AddEngineModule(new InstalledEngineModule(moduleName, moduleVersion));
        _cfg.CommitConfig();

        Log.Debug("Done installing module!");

        return true;

    }

    private async Task CopyOverrideModule(string name)
    {
        GetModulePaths(
            name,
            OverrideVersionName,
            out var modPath,
            out var modVersionPath);

        await ClearModuleDir(modPath, modVersionPath);

        var zipPath = FindOverrideZip(name, _cfg.GetCVar(CVars.EngineOverridePath));
        using var zip = File.OpenRead(zipPath);

        // Note: not marking directory as compressed since it would take a while to start.
        ExtractModule(name, modVersionPath, zip);
    }

    private static void GetModulePaths(
        string module,
        string version,
        out string moduleDiskPath,
        out string moduleVersionDiskPath)
    {
        moduleDiskPath = Path.Combine(LauncherPaths.DirModuleInstallations, module);
        moduleVersionDiskPath = Path.Combine(moduleDiskPath, version);
    }

    private static async Task ClearModuleDir(string modDiskPath, string modVersionDiskPath)
    {
        await Task.Run(() =>
        {
            // Avoid disk IO hang.
            Helpers.EnsureDirectoryExists(modDiskPath);
            Helpers.EnsureDirectoryExists(modVersionDiskPath);
            Helpers.ClearDirectory(modVersionDiskPath);
        }, CancellationToken.None);
    }

    private static void ExtractModule(string moduleName, string moduleVersionDiskPath, FileStream tempFile)
    {
        Helpers.ExtractZipToDirectory(moduleVersionDiskPath, tempFile);

        // Chmod required files.
        if (OperatingSystem.IsLinux())
        {
            switch (moduleName)
            {
                case "Robust.Client.WebView":
                    Helpers.ChmodPlusX(Path.Combine(moduleVersionDiskPath, "Robust.Client.WebView"));
                    break;
            }
        }
    }

    private static unsafe bool VerifyModuleSignature(FileStream stream, string module, string signature)
    {
        if (stream.Length > int.MaxValue)
            throw new InvalidOperationException("Unable to handle files larger than 2 GiB");

        // Use memory-mapped file here so we don't have to read the whole thing in at once.
        using var memoryMapped = MemoryMappedFile.CreateFromFile(
            stream,
            null,
            0,
            MemoryMappedFileAccess.Read,
            HandleInheritability.None,
            leaveOpen: true);

        using var accessor = memoryMapped.CreateViewAccessor(0, stream.Length, MemoryMappedFileAccess.Read);
        byte* ptr = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

        try
        {
            var span = new ReadOnlySpan<byte>(ptr, (int)stream.Length);

            var pubKey = PublicKey.Import(
                SignatureAlgorithm.Ed25519,
                File.ReadAllBytes(LauncherPaths.PathPublicKeys!.GetValueOrDefault(module, null) ?? LauncherPaths.PathPublicKey),
                KeyBlobFormat.PkixPublicKeyText);

            var sigBytes = Convert.FromHexString(signature);

            return SignatureAlgorithm.Ed25519.Verify(pubKey, span, sigBytes);
        }
        finally
        {
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    public async Task<EngineModuleManifest> GetEngineModuleManifest(string engine, CancellationToken cancel = default)
    {
        if (!ConfigConstants.EngineModulesUrl.TryGetValue(engine, out var urls))
            throw new InvalidOperationException("No manifest URL for engine module");

        if (await urls.GetFromJsonAsync<EngineModuleManifest>(_http, cancel) is { } manifest)
            return manifest;

        throw new InvalidOperationException("Failed to download engine module manifest");
    }

    public async Task DoEngineCullMaybeAsync(SqliteConnection contenCon)
    {
        Log.Debug("Checking to cull engine dependencies");

        // Cull main engine installations.

        var origModulesUsed = contenCon
            .Query<(string, string)>("SELECT DISTINCT ModuleName, ModuleVersion FROM ContentEngineDependency")
            .ToList();

        // GOD DAMNIT more bodging everything together.
        // The code sucks.
        // My shitty hacks to do engine version redirection fall apart here as well.
        var modulesUsed = new HashSet<(string, string)>();
        foreach (var (name, version) in origModulesUsed)
        {
            if (name == "Robust" && await GetVersionInfo(version, name) is { } redirect)
            {
                modulesUsed.Add(("Robust", redirect.Version));
            }
            else
            {
                modulesUsed.Add((name, version));
            }
        }

        var toCull = _cfg.EngineInstallations.Items.Where(i => !modulesUsed.Any(m => m.Item2 == i.Version)).ToArray();

        foreach (var installation in toCull)
        {
            Log.Debug("Culling unused version {engineVersion}", installation.Version);

            var path = GetEnginePath(installation.Version);

            _cfg.RemoveEngineInstallation(installation);

            await Task.Run(() => File.Delete(path));
        }

        // Cull modules
        var toCullModules = _cfg.EngineModules.Where(m => !modulesUsed.Contains((m.Name, m.Version))).ToArray();

        foreach (var module in toCullModules)
        {
            Log.Debug("Culling unused module {EngineModule}", module);

            var path = GetEngineModule(module.Name, module.Version);

            _cfg.RemoveEngineModule(module);

            await Task.Run(() => Directory.Delete(path, true));
        }
    }

    public void ClearAllEngines()
    {
        foreach (var install in _cfg.EngineInstallations.Items.ToArray())
        {
            _cfg.RemoveEngineInstallation(install);
        }

        foreach (var module in _cfg.EngineModules.ToArray())
        {
            _cfg.RemoveEngineModule(module);
        }

        foreach (var file in Directory.EnumerateFiles(LauncherPaths.DirEngineInstallations))
        {
            File.Delete(file);
        }

        foreach (var dir in Directory.EnumerateFiles(LauncherPaths.DirModuleInstallations))
        {
            Directory.Delete(dir, recursive: true);
        }

        _cfg.CommitConfig();
    }

    private static string FindOverrideZip(string name, string dir)
    {
        var foundRids = new List<string>();

        var regex = new Regex(@$"^{Regex.Escape(name)}_([a-z\-\d]+)\.zip$");
        foreach (var item in Directory.EnumerateFiles(dir))
        {
            var fileName = Path.GetFileName(item);
            var match = regex.Match(fileName);
            if (!match.Success)
                continue;

            foundRids.Add(match.Groups[1].Value);
        }

        var rid = RidUtility.FindBestRid(foundRids);
        if (rid == null)
            throw new UpdateException($"Unable to find overriden {name} for current platform {RidUtility.GuessRid()}, found: {string.Join(", ", foundRids)}");

        var path = Path.Combine(dir, $"{name}_{rid}.zip");
        Log.Warning("Using override for {Name}: {Path}", name, path);
        return path;
    }
}
