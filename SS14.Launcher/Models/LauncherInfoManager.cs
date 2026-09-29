using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;

namespace SS14.Launcher.Models;

/// <summary>
/// Fetches and caches information from <see cref="ConfigConstants.UrlLauncherInfo"/>.
/// </summary>
public sealed class LauncherInfoManager(HttpClient httpClient)
{
    private LauncherInfoModel? _model;

    public LauncherInfoModel? Model
    {
        get
        {
            if (!LoadTask.IsCompleted)
                throw new InvalidOperationException("Data has not been loaded yet");

            return _model;
        }
    }

    public Task LoadTask { get; private set; } = default!;

    public void Initialize()
    {
        LoadTask = LoadData();
    }

    private async Task LoadData()
    {
        // Detached from upstream: the info file carried their version list, announcements and
        // branding overrides, none of which apply to this fork. Nothing is fetched, so nothing
        // downstream ever sees a model and every consumer takes its "no info" path.
        if (!ConfigConstants.DoVersionCheck)
            return;

        LauncherInfoModel? info;
        try
        {
            Log.Debug("Loading launcher info... {Url}", ConfigConstants.UrlLauncherInfo);
            info = await ConfigConstants.UrlLauncherInfo.GetFromJsonAsync<LauncherInfoModel>(httpClient);
            if (info == null)
            {
                Log.Warning("Launcher info response was null.");
                return;
            }
        }
        catch (Exception e)
        {
            Log.Warning(e, "Loading launcher info failed");
            return;
        }

        // NOTE: info.Messages is deliberately ignored, the loading screen uses LoadingMessages instead.
        _model = info;
    }

    public string GetRandomMessage()
    {
        return LoadingMessages.Get();
    }

    public sealed class CustomInfo
    {
        public string Message { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LinkText { get; set; } = "Open Link";
        public string Link { get; set; } = string.Empty;

        public static readonly CustomInfo None = new();
    }

    public sealed record LauncherInfoModel(
        Dictionary<string, string[]> Messages,
        string[] AllowedVersions,
        CustomInfo CustomInfo,
        Dictionary<string, string?> OverrideAssets
    );
}
