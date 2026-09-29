using System;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models.Data;

/// <summary>
/// Contains definitions for all launcher configuration values.
/// </summary>
/// <remarks>
/// The fields of this class are automatically searched for all CVar definitions.
/// </remarks>
/// <see cref="DataManager"/>
[UsedImplicitly]
public static class CVars
{
    /// <summary>
    /// Default to using compatibility options for rendering etc,
    /// that are less likely to immediately crash on buggy drivers.
    /// </summary>
    public static readonly CVarDef<bool> CompatMode = CVarDef.Create("CompatMode", false);

    /// <summary>
    /// Used to warn users about the degradation of the Intel 13th and 14th generation CPUs
    /// This has proven multiple times to cause issues with game startup due to some memory access issue after enough degradation.
    /// <see href="https://www.reddit.com/r/intel/comments/1egthzw/megathread_for_intel_core_13th_14th_gen_cpu/"/>
    /// </summary>
    public static readonly CVarDef<bool> HasDismissedIntelDegradation
        = CVarDef.Create("HasDismissedIntelDegradation", false);

    /// <summary>
    /// Used to warn Apple Silicon users who are running the game under Rosetta 2 when they could be running the native build.
    /// </summary>
    public static readonly CVarDef<bool> HasDismissedRosettaWarning
        = CVarDef.Create("HasDismissedRosettaWarning", false);

    /// <summary>
    /// Disable checking engine build signatures when launching game.
    /// Only enable if you know what you're doing.
    /// </summary>
    /// <remarks>
    /// This is ignored on release builds, for security reasons.
    /// </remarks>
    public static readonly CVarDef<bool> DisableSigning = CVarDef.Create("DisableSigning", false);

    /// <summary>
    /// Enable local overriding of engine versions.
    /// </summary>
    /// <remarks>
    /// If enabled and on a development build,
    /// the launcher will pull all engine versions and modules from <see cref="EngineOverridePath"/>.
    /// This can be set to <c>RobustToolbox/release/</c> to instantly pull in packaged engine builds.
    /// </remarks>
    public static readonly CVarDef<bool> EngineOverrideEnabled = CVarDef.Create("EngineOverrideEnabled", false);

    /// <summary>
    /// Path to load engines from when using <see cref="EngineOverrideEnabled"/>.
    /// </summary>
    public static readonly CVarDef<string> EngineOverridePath = CVarDef.Create("EngineOverridePath", "");

    /// <summary>
    /// Verbose logging of launcher logs.
    /// </summary>
    public static readonly CVarDef<bool> LogLauncherVerbose = CVarDef.Create("LogLauncherVerbose", true);

    /// <summary>
    /// Enable multi-account support on release builds.
    /// </summary>
    public static readonly CVarDef<bool> MultiAccounts = CVarDef.Create("MultiAccounts", true);

    /// <summary>
    /// Currently selected login in the drop down.
    /// </summary>
    public static readonly CVarDef<string> SelectedLogin = CVarDef.Create("SelectedLogin", "");

    public static readonly CVarDef<string> Fingerprint = CVarDef.Create("Fingerprint", "");

    /// <summary>
    /// Maximum amount of TOTAL versions to keep in the content database.
    /// </summary>
    public static readonly CVarDef<int> MaxVersionsToKeep = CVarDef.Create("MaxVersionsToKeep", 15);

    /// <summary>
    /// Maximum amount of versions to keep of a specific fork ID.
    /// </summary>
    public static readonly CVarDef<int> MaxForkVersionsToKeep = CVarDef.Create("MaxForkVersionsToKeep", 3);

    public static readonly CVarDef<double> UiScalingX = CVarDef.Create("UiScalingX", 1.0);
    public static readonly CVarDef<double> UiScalingY = CVarDef.Create("UiScalingY", 1.0);
    /// <summary>
    /// Whether the UI scaling options should always be the same as each other.
    /// </summary>
    public static readonly CVarDef<bool> UiScalingLock = CVarDef.Create("UiScalingLock", true);

     /// <summary>
    /// If a download gets interrupted, keep the files for a week.
    /// </summary>
    public static readonly CVarDef<int> InterruptibleDownloadKeepHours = CVarDef.Create("InterruptibleDownloadKeepHours", 7 * 24);

    /// <summary>
    /// Whether to display remotely delivered "seasonal branding" assets.
    /// </summary>
    /// <remarks>
    /// Disabled in this build: <see cref="OverrideAssets"/> downloads icons and logos from a remote
    /// server that can change them at any time, so the launcher only ever shows its own bundled art.
    /// </remarks>
    public static readonly CVarDef<bool> OverrideAssets = CVarDef.Create("OverrideAssets", false);

    /// <summary>
    /// Stores the minimum player count value used by the "minimum player count" filter.
    /// </summary>
    /// <seealso cref="ServerFilter.PlayerCountMin"/>
    public static readonly CVarDef<int> FilterPlayerCountMinValue = CVarDef.Create("FilterPlayerCountMinValue", 0);

    /// <summary>
    /// Stores the maximum player count value used by the "maximum player count" filter.
    /// </summary>
    /// <seealso cref="ServerFilter.PlayerCountMax"/>
    public static readonly CVarDef<int> FilterPlayerCountMaxValue = CVarDef.Create("FilterPlayerCountMaxValue", 0);

    /// <summary>
    /// Stores whether the user has seen the Wine warning.
    /// </summary>
    public static readonly CVarDef<bool> WineWarningShown = CVarDef.Create("WineWarningShown", false);

    /// <summary>
    /// Whether the character editor fetches many files at once. Off falls back to one at a time,
    /// which is far slower but gentler on connections that dislike parallel requests.
    /// </summary>
    public static readonly CVarDef<bool> CharacterFastDownload = CVarDef.Create("CharacterFastDownload", true);

    /// <summary>
    /// Servers the round watcher polls, as a JSON list of addresses.
    /// </summary>
    public static readonly CVarDef<string> WatchedServers = CVarDef.Create("WatchedServers", "[]");

    /// <summary>
    /// Notify when a watched server reaches this many players; 0 turns that off.
    /// </summary>
    public static readonly CVarDef<int> WatchPlayerThreshold = CVarDef.Create("WatchPlayerThreshold", 0);

    /// <summary>
    /// Connect to the last played server as soon as the launcher is ready.
    /// </summary>
    public static readonly CVarDef<bool> AutoConnectLast = CVarDef.Create("AutoConnectLast", false);

    /// <summary>
    /// The player's own notes about servers, as a JSON map of address to note.
    /// See <see cref="ServerNotes"/>.
    /// </summary>
    public static readonly CVarDef<string> ServerNotes = CVarDef.Create("ServerNotes", "{}");

    /// <summary>
    /// Whether the launcher has already offered to set up the blocking bypass. Offered once.
    /// </summary>
    public static readonly CVarDef<bool> ZapretOffered = CVarDef.Create("ZapretOffered", false);

    /// <summary>
    /// Which of zapret's strategy scripts to run.
    /// </summary>
    public static readonly CVarDef<string> ZapretStrategy = CVarDef.Create("ZapretStrategy", "general.bat");

    /// <summary>
    /// Start the bypass along with the launcher.
    /// </summary>
    public static readonly CVarDef<bool> ZapretAutoStart = CVarDef.Create("ZapretAutoStart", false);

    /// <summary>
    /// Whether a picture is drawn behind the launcher. See <see cref="LauncherBackground"/>.
    /// </summary>
    public static readonly CVarDef<bool> BackgroundEnabled = CVarDef.Create("BackgroundEnabled", false);

    /// <summary>
    /// How strongly the background picture shows through, 0 to 100.
    /// </summary>
    public static readonly CVarDef<int> BackgroundStrength = CVarDef.Create("BackgroundStrength", 35);

    /// <summary>
    /// How much darkness is laid over the background picture, 0 to 100.
    /// </summary>
    public static readonly CVarDef<int> BackgroundDim = CVarDef.Create("BackgroundDim", 35);

    /// <summary>
    /// Crop the background to fill the window rather than fitting it whole.
    /// </summary>
    public static readonly CVarDef<bool> BackgroundFill = CVarDef.Create("BackgroundFill", true);

    /// <summary>
    /// Per-server playtime totals, as JSON. See <see cref="GameStatistics"/>.
    /// </summary>
    public static readonly CVarDef<string> GameStats = CVarDef.Create("GameStats", "[]");

    /// <summary>
    /// Servers recently connected to, as JSON. See <see cref="RecentServers"/>.
    /// </summary>
    public static readonly CVarDef<string> RecentServers = CVarDef.Create("RecentServers", "[]");

    /// <summary>
    /// How the server list is ordered, see <c>ServerListTabViewModel.SortOptions</c>.
    /// </summary>
    public static readonly CVarDef<string> ServerListSort = CVarDef.Create("ServerListSort", "players");

    /// <summary>
    /// Colour palette the launcher UI uses, see <see cref="ThemeManager.Themes"/>.
    /// </summary>
    public static readonly CVarDef<string> Theme = CVarDef.Create("Theme", ThemeManager.DefaultThemeId);

    /// <summary>
    /// Language the user selected. Null means it should be automatically selected based on system language.
    /// </summary>
    public static readonly CVarDef<string?> Language = CVarDef.Create<string?>("Language", null);

    /// <summary>
    /// The CPU architecture this launcher was last run with.
    /// </summary>
    /// <remarks>
    /// Used to delete engine builds of other architectures on startup.
    /// Defaults to x64 so that people upgrading to a proper ARM64 launcher on e.g. Apple Silicon
    /// properly get their existing installations cleared.
    /// </remarks>
    public static readonly CVarDef<int> CurrentArchitecture = CVarDef.Create("CurrentArchitecture", (int) Architecture.X64);
}

/// <summary>
/// Base definition of a CVar.
/// </summary>
/// <seealso cref="DataManager"/>
/// <seealso cref="CVars"/>
public abstract class CVarDef
{
    public string Name { get; }
    public object? DefaultValue { get; }
    public Type ValueType { get; }

    private protected CVarDef(string name, object? defaultValue, Type type)
    {
        Name = name;
        DefaultValue = defaultValue;
        ValueType = type;
    }

    public static CVarDef<T> Create<T>(
        string name,
        T defaultValue)
    {
        return new CVarDef<T>(name, defaultValue);
    }
}

/// <summary>
/// Generic specialized definition of CVar definition.
/// </summary>
/// <typeparam name="T">The type of value stored in this CVar.</typeparam>
public sealed class CVarDef<T> : CVarDef
{
    public new T DefaultValue { get; }

    internal CVarDef(string name, T defaultValue) : base(name, defaultValue, typeof(T))
    {
        DefaultValue = defaultValue;
    }
}
