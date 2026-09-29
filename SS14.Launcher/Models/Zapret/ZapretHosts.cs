using System.Collections.Generic;
using System.Linq;

namespace SS14.Launcher.Models.Zapret;

/// <summary>
/// The hosts a Space Station 14 player needs reachable, for zapret's user host list.
/// </summary>
/// <remarks>
/// The upstream lists cover Discord and YouTube; none of them mention game hubs, auth servers or
/// the CDNs that serve the engine and server content. Those are exactly what gets throttled here,
/// so the launcher writes its own list into the user file the scripts already load, leaving every
/// file that ships with zapret untouched.
/// </remarks>
public static class ZapretHosts
{
    /// <summary>
    /// Header written above the generated list, so anyone opening the file knows who wrote it.
    /// </summary>
    public const string Header = "# Written by Airlock Launcher: hosts Space Station 14 needs.";

    /// <summary>
    /// Domains, deduplicated and sorted. Suffix matching is how zapret reads these, so the
    /// registrable domain covers its subdomains.
    /// </summary>
    public static IReadOnlyList<string> Domains { get; } = Build();

    private static string[] Build()
    {
        var hosts = new List<string>
        {
            // Hubs: the server lists themselves.
            "spacestation14.com",
            "hub.spacestation14.com",
            "playss14.com",
            "simplestation.org",
            "spacestationmultiverse.com",
            "singularity14.co.uk",

            // Accounts.
            "account.spacestation14.com",
            "auth.spacestation14.com",

            // Engine builds and content CDNs. Named in full rather than left to the parent
            // domain: these are the hosts that actually get throttled, and someone reading this
            // list should see them.
            "cdn.spacestation14.com",
            "robust-builds.cdn.spacestation14.com",
            "robust-builds.fallback.cdn.spacestation14.com",
            "robust-builds.playss14.com",
            "launcher-data.cdn.spacestation14.com",
            "wizards.cdn.spacestation14.com",
            "central.spacestation14.io",
            "spacestation14.io",

            // Amazon's delivery network, which several forks host their content behind. The shop
            // domain is in the list because it was asked for; the two below it are the ones that
            // actually serve game files.
            "amazon.com",
            "amazonaws.com",
            "cloudfront.net",

            // Where the launcher pulls sprites and prototypes for the character editor.
            "github.com",
            "api.github.com",
            "raw.githubusercontent.com",
            "objects.githubusercontent.com",
            "codeload.github.com",
            "cdn.jsdelivr.net",

            // Russian-speaking servers people actually play on.
            "station14.ru",
            "deadspace14.net",
            "ss14.org",
            "reserve-station.space",
            "lust-station.space",
            "ss14.com.ua",
            "ss14.su",
        };

        return hosts
            .Select(host => host.Trim().ToLowerInvariant())
            .Where(host => host.Length > 0)
            .Distinct()
            .OrderBy(host => host, System.StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// The finished file: a comment line, then one host per line.
    /// </summary>
    public static string FileContents()
    {
        return Header + "\n" + string.Join("\n", Domains) + "\n";
    }
}
