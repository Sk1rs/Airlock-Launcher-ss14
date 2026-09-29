using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace SS14.Launcher.Models.Character;

/// <summary>
/// The fork's own translations, so species, markings and jobs are named the way that fork names them.
/// </summary>
/// <remarks>
/// Prototypes carry translation keys rather than words, and every fork ships its own wording, so
/// this reads the fork's locale files instead of the launcher's. Not every fork translates into
/// every language, hence the fall back to its English, and then to the prototype id.
/// </remarks>
public sealed partial class ForkLocale
{
    /// <summary>
    /// Locale folders worth reading: names of species, markings and jobs, and nothing else.
    /// </summary>
    [GeneratedRegex(@"^Resources/Locale/(?<culture>[^/]+)/(species|markings|job)/.*\.ftl$")]
    private static partial Regex LocaleFileRegex { get; }

    /// <summary>
    /// A fluent line: <c>key = value</c>, ignoring comments, attributes and continuations.
    /// </summary>
    [GeneratedRegex(@"(?m)^([a-zA-Z][\w-]*)\s*=\s*(.+?)\s*$")]
    private static partial Regex EntryRegex { get; }

    private const string FallbackCulture = "en-US";

    private readonly Dictionary<string, string> _entries = new();
    private readonly Dictionary<string, string> _fallback = new();

    /// <summary>
    /// Which locale folder holds a launcher language in the content repositories.
    /// </summary>
    public static string CultureFolder(string language) => language switch
    {
        "ru" => "ru-RU",
        "pl" => "pl-PL",
        _ => FallbackCulture,
    };

    /// <summary>
    /// The translation for a key, or null when nobody has one.
    /// </summary>
    public string? Get(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (_entries.TryGetValue(key, out var value))
            return value;

        return _fallback.GetValueOrDefault(key);
    }

    /// <summary>
    /// Downloads the fork's name files for a language, plus its English ones to fall back on.
    /// </summary>
    public async Task LoadAsync(
        ForkResources resources,
        string[] files,
        string language,
        bool fast,
        CancellationToken cancel = default)
    {
        var culture = CultureFolder(language);

        var wanted = new List<string>();
        var fallbackFiles = new List<string>();

        foreach (var file in files)
        {
            if (LocaleFileRegex.Match(file) is not { Success: true } match)
                continue;

            var fileCulture = match.Groups["culture"].Value;

            if (fileCulture == culture)
                wanted.Add(file);
            else if (fileCulture == FallbackCulture)
                fallbackFiles.Add(file);
        }

        // Without a repository listing there is nothing to filter, so fall back on the paths every
        // fork inherits from upstream. Files a fork does not have simply answer with nothing.
        if (wanted.Count == 0 && fallbackFiles.Count == 0)
        {
            foreach (var known in KnownPaths.LocaleFiles)
            {
                wanted.Add($"Resources/Locale/{culture}/{known}");

                if (culture != FallbackCulture)
                    fallbackFiles.Add($"Resources/Locale/{FallbackCulture}/{known}");
            }
        }

        // Reading the fork's English too costs little and is what keeps an untranslated marking
        // from showing up as a bare prototype id.
        var texts = await resources.GetTextFilesAsync(
            wanted.Concat(fallbackFiles).ToArray(),
            fast,
            cancel: cancel);

        foreach (var (path, text) in texts)
        {
            var target = wanted.Contains(path) ? _entries : _fallback;

            foreach (Match entry in EntryRegex.Matches(text))
            {
                target[entry.Groups[1].Value] = entry.Groups[2].Value;
            }
        }

        Log.Information(
            "Loaded {Count} names in {Culture} and {Fallback} in {FallbackCulture} for {Fork}",
            _entries.Count,
            culture,
            _fallback.Count,
            FallbackCulture,
            resources.Fork.Id);
    }

    /// <summary>
    /// Pulls the entries out of a fluent file. Exposed for testing.
    /// </summary>
    public static Dictionary<string, string> ParseEntries(string text)
    {
        var entries = new Dictionary<string, string>();

        foreach (Match match in EntryRegex.Matches(text))
        {
            entries[match.Groups[1].Value] = match.Groups[2].Value;
        }

        return entries;
    }
}
