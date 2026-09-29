using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;
using SS14.Launcher.Localization;
using SS14.Launcher.Models;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

/// <summary>
/// Wikis and role guides, plus the way to the screenshots folder.
/// </summary>
public sealed class GuidesTabViewModel : MainWindowTabViewModel
{
    private readonly LocalizationManager _loc = LocalizationManager.Instance;

    public override string Name => _loc.GetString("tab-guides-title");

    public IReadOnlyList<GuideSectionViewModel> Sections { get; }

    public GuidesTabViewModel()
    {
        Sections = GuideLinks.Sections
            .Select(section => new GuideSectionViewModel(
                _loc.GetString(section.TitleKey),
                section.Links.Select(link => new GuideLinkViewModel(_loc.GetString(link.TitleKey), link)).ToList()))
            .ToList();
    }

    /// <summary>
    /// Where screenshots from games started here end up. Made on demand, so the button works
    /// before the first screenshot is ever taken.
    /// </summary>
    public static string ScreenshotsDirectory => LauncherDataFolders.Own(GuideLinks.ScreenshotsFolder);

    public static void OpenScreenshotsFolder() => Helpers.OpenFolder(ScreenshotsDirectory);
}

public sealed class GuideSectionViewModel(string title, IReadOnlyList<GuideLinkViewModel> links)
{
    public string Title { get; } = title;

    public IReadOnlyList<GuideLinkViewModel> Links { get; } = links;
}

public sealed class GuideLinkViewModel(string title, GuideLink link)
{
    public string Title { get; } = title;

    public string Language { get; } = link.Language;

    public string Url { get; } = link.Url;

    public void Open() => Helpers.OpenUri(new Uri(Url));
}
