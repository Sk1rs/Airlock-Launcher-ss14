using System;
using System.Collections.Immutable;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Serilog;
using Splat;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher;

/// <summary>
/// Applies the colour palette the user picked in the options.
/// </summary>
/// <remarks>
/// Every palette in <c>Theme/Palettes</c> defines the same resource keys, and the rest of the UI only
/// refers to them through <c>DynamicResource</c>. Swapping the palette style in
/// <see cref="Application.Styles"/> therefore recolours the launcher without a restart.
/// </remarks>
public sealed class ThemeManager
{
    public const string DefaultThemeId = "Default";

    /// <summary>
    /// Themes offered in the options, in display order. The id is the palette's file name.
    /// </summary>
    public static readonly ImmutableArray<string> Themes =
    [
        DefaultThemeId,
        "Midnight",
        "Slate",
        "Nanotrasen",
        "Amber",
        "Syndicate",
        "Atmospherics",
        "Hydroponics",
        "Epistemics",
        "Daylight",
        "Oceanic",
        "Sakura",
        "Rust",
        "Monochrome",
    ];

    private const string PaletteDir = "/Theme/Palettes/";

    private readonly DataManager _cfg;

    public ThemeManager(DataManager cfg)
    {
        _cfg = cfg;
    }

    public static ThemeManager Instance => Locator.Current.GetRequiredService<ThemeManager>();

    public string CurrentTheme
    {
        get
        {
            var id = _cfg.GetCVar(CVars.Theme);

            return Themes.Contains(id) ? id : DefaultThemeId;
        }
    }

    /// <summary>
    /// Applies the theme stored in the config. Call once at startup.
    /// </summary>
    public void Initialize()
    {
        Apply(CurrentTheme);
    }

    /// <summary>
    /// Saves the given theme and applies it immediately.
    /// </summary>
    public void SetTheme(string id)
    {
        if (!Themes.Contains(id))
            throw new ArgumentException($"Unknown theme: {id}", nameof(id));

        _cfg.SetCVar(CVars.Theme, id);
        _cfg.CommitConfig();

        Apply(id);
    }

    private static void Apply(string id)
    {
        if (Application.Current is not { } app)
            return;

        Log.Debug("Applying theme {Theme}", id);

        // Drop whichever palette is loaded right now, including the default one from App.xaml.
        foreach (var old in app.Styles
                     .OfType<StyleInclude>()
                     .Where(style => style.Source?.OriginalString.Contains(PaletteDir) ?? false)
                     .ToArray())
        {
            app.Styles.Remove(old);
        }

        // Added last so the palette wins over the base theme's resources.
        app.Styles.Add(new StyleInclude(new Uri("avares://SS14.Launcher/"))
        {
            Source = new Uri($"avares://SS14.Launcher{PaletteDir}{id}.xaml"),
        });
    }
}
