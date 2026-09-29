using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace SS14.Launcher.Models.Character;

/// <summary>
/// Fetches the handful of files the character editor needs out of a content repository, and keeps
/// them on disk so it only happens once.
/// </summary>
/// <remarks>
/// Cloning these repositories is out of the question: they are gigabytes and mostly maps and audio.
/// Instead the file list comes from one call to the git tree API and individual files are pulled
/// from raw.githubusercontent as they are needed.
/// </remarks>
public sealed class ForkResources
{
    private readonly CharacterFork _fork;
    private readonly HttpClient _http;
    private readonly string _cacheDir;

    private string[]? _files;

    public ForkResources(CharacterFork fork, HttpClient http)
    {
        _fork = fork;
        _http = http;
        _cacheDir = Path.Combine(LauncherPaths.DirUserData, "character", fork.Id);
    }

    public CharacterFork Fork => _fork;

    private string TreeCachePath => Path.Combine(_cacheDir, "tree.json");

    private string FileCachePath(string path)
        => Path.Combine(_cacheDir, "files", path.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// Every file path in the repository, or null when GitHub would not give us the list.
    /// </summary>
    /// <remarks>
    /// The listing costs one call against GitHub's unauthenticated budget of sixty an hour per
    /// address, which a shared connection can burn through without the user doing anything. It is
    /// cached forever once fetched, and callers are expected to cope with null by falling back on
    /// known paths.
    /// </remarks>
    public async Task<string[]?> GetFileListAsync(CancellationToken cancel = default)
    {
        try
        {
            return await FetchFileListAsync(cancel);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException)
        {
            Log.Warning(e, "Could not get the file list for {Fork}, falling back on known paths", _fork.Id);

            return null;
        }
    }

    private async Task<string[]> FetchFileListAsync(CancellationToken cancel)
    {
        if (_files != null)
            return _files;

        if (File.Exists(TreeCachePath))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(TreeCachePath, cancel));
                if (cached is { Length: > 0 })
                    return _files = cached;
            }
            catch (Exception e)
            {
                Log.Warning(e, "Cached file list for {Fork} is unreadable, fetching again", _fork.Id);
            }
        }

        Log.Information("Fetching file list for {Fork}", _fork.Id);

        using var request = new HttpRequestMessage(HttpMethod.Get, _fork.TreeUrl);

        // The GitHub API turns away anything without a user agent.
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            request.Headers.UserAgent.ParseAdd("SS14.Launcher");

        using var response = await _http.SendAsync(request, cancel);
        response.EnsureSuccessStatusCode();

        var tree = await ReadTreeAsync(response.Content, cancel);

        var files = tree?.Tree?
            .Where(entry => entry.Type == "blob" && entry.Path != null)
            .Select(entry => entry.Path!)
            .ToArray() ?? [];

        if (files.Length == 0)
            throw new InvalidDataException($"Got an empty file list for {_fork.Id}");

        Directory.CreateDirectory(_cacheDir);
        await File.WriteAllTextAsync(TreeCachePath, JsonSerializer.Serialize(files), cancel);

        return _files = files;
    }

    /// <summary>
    /// Fetches one file from the repository, from disk if it was fetched before.
    /// </summary>
    /// <returns>Null if the repository does not have it.</returns>
    public async Task<byte[]?> GetFileAsync(string path, CancellationToken cancel = default)
    {
        var cachePath = FileCachePath(path);

        if (File.Exists(cachePath))
            return await File.ReadAllBytesAsync(cachePath, cancel);

        // raw.githubusercontent first, then the CDN mirror: neither is rate limited the way the API
        // is, but one of them being unreachable shouldn't take the editor down with it.
        foreach (var url in new[] { _fork.RawUrl(path), _fork.CdnUrl(path) })
        {
            try
            {
                using var response = await _http.GetAsync(url, cancel);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Log.Verbose("{Fork} has no {Path}", _fork.Id, path);
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Log.Debug("{Url} returned {Status}", url, response.StatusCode);
                    continue;
                }

                var data = await response.Content.ReadAsByteArrayAsync(cancel);

                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                await File.WriteAllBytesAsync(cachePath, data, cancel);

                return data;
            }
            catch (Exception e) when (e is HttpRequestException or IOException)
            {
                Log.Warning(e, "Failed to fetch {Path} from {Url}", path, url);
            }
        }

        return null;
    }

    public async Task<string?> GetTextAsync(string path, CancellationToken cancel = default)
    {
        var data = await GetFileAsync(path, cancel);

        return data == null ? null : System.Text.Encoding.UTF8.GetString(data);
    }

    /// <summary>
    /// How many files the fast path asks for at once.
    /// </summary>
    /// <remarks>
    /// The editor needs a few hundred small files, and fetched one after another that is minutes of
    /// waiting on round trips rather than bandwidth. Sixteen at a time keeps both GitHub and the
    /// user's router content.
    /// </remarks>
    public const int FastConcurrency = 16;

    /// <summary>
    /// Fetches many files, in parallel when asked to.
    /// </summary>
    /// <returns>Text by path; files the repository does not have are left out.</returns>
    public async Task<Dictionary<string, string>> GetTextFilesAsync(
        IReadOnlyCollection<string> paths,
        bool parallel,
        IProgress<int>? progress = null,
        CancellationToken cancel = default)
    {
        var results = new Dictionary<string, string>();
        var done = 0;

        if (!parallel)
        {
            foreach (var path in paths)
            {
                cancel.ThrowIfCancellationRequested();

                if (await GetTextAsync(path, cancel) is { } text)
                    results[path] = text;

                progress?.Report(++done);
            }

            return results;
        }

        using var limit = new SemaphoreSlim(FastConcurrency, FastConcurrency);
        var gate = new object();

        var tasks = paths.Select(async path =>
        {
            await limit.WaitAsync(cancel);
            try
            {
                var text = await GetTextAsync(path, cancel);

                lock (gate)
                {
                    if (text != null)
                        results[path] = text;

                    progress?.Report(++done);
                }
            }
            finally
            {
                limit.Release();
            }
        });

        await Task.WhenAll(tasks);

        return results;
    }

    /// <summary>
    /// Throws away everything downloaded for this fork.
    /// </summary>
    public void Clear()
    {
        _files = null;

        try
        {
            if (Directory.Exists(_cacheDir))
                Directory.Delete(_cacheDir, recursive: true);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to clear cached resources for {Fork}", _fork.Id);
        }
    }

    public long CachedBytes()
    {
        try
        {
            if (!Directory.Exists(_cacheDir))
                return 0;

            return new DirectoryInfo(_cacheDir)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(file => file.Length);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to measure cached resources for {Fork}", _fork.Id);
            return 0;
        }
    }

    private sealed class GitTree
    {
        [JsonPropertyName("tree")] public List<GitTreeEntry>? Tree { get; set; }
    }

    private sealed class GitTreeEntry
    {
        [JsonPropertyName("path")] public string? Path { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
    }

    private static async Task<GitTree?> ReadTreeAsync(HttpContent content, CancellationToken cancel)
    {
        await using var stream = await content.ReadAsStreamAsync(cancel);

        return await JsonSerializer.DeserializeAsync<GitTree>(stream, cancellationToken: cancel);
    }
}
