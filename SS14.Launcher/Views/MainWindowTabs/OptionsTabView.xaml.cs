using System;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Models;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;
using SS14.Launcher.ViewModels.MainWindowTabs;

namespace SS14.Launcher.Views.MainWindowTabs;

public partial class OptionsTabView : UserControl
{
    public DataManager Cfg { get; }
    private OptionsTabViewModel Data => (OptionsTabViewModel) DataContext!;

    public OptionsTabView()
    {
        Cfg = Locator.Current.GetRequiredService<DataManager>();
        InitializeComponent();

        Flip.Command = ReactiveCommand.Create(() =>
        {
            var window = (Window?) VisualRoot;
            if (window == null)
                return;

            window.Classes.Add("DoAFlip");

            DispatcherTimer.RunOnce(() => { window.Classes.Remove("DoAFlip"); }, TimeSpan.FromSeconds(1));
        });
    }

    public void ApplyUiScaling(object? sender, RoutedEventArgs args)
    {
        Cfg.SetCVar(CVars.UiScalingX, Data.UiScalingX);
        Cfg.SetCVar(CVars.UiScalingY, Data.UiScalingY);
        Cfg.CommitConfig();
    }

    public async void ClearEnginesPressed(object? _1, RoutedEventArgs _2)
    {
        Data.ClearEngines();
        await ClearEnginesButton.DisplayDoneMessage();
    }

    public async void ClearServerContentPressed(object? _1, RoutedEventArgs _2)
    {
        var blocked = !await ((OptionsTabViewModel)DataContext!).ClearServerContent();
        var locMgr = Locator.Current.GetService<LocalizationManager>()!;

        await ClearServerContentButton.DisplayDoneMessage(
            blocked ? locMgr.GetString("tab-options-clear-content-close-client") : null);
    }

    private async void RefreshStorage(object? sender, RoutedEventArgs args)
    {
        if (DataContext is OptionsTabViewModel vm)
            await vm.RefreshStorage();
    }

    private async void CopyDiagnostics(object? sender, RoutedEventArgs args)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            return;

        await clipboard.SetTextAsync(DiagnosticsReport.Build());
    }

    private async void PickBackground(object? sender, RoutedEventArgs args)
    {
        if (TopLevel.GetTopLevel(this) is not { } top)
            return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.ImageAll],
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
            LauncherBackground.Instance.Set(path);
    }

    private void ClearBackground(object? sender, RoutedEventArgs args)
    {
        LauncherBackground.Instance.Clear();
    }

    private async void OpenZapret(object? sender, RoutedEventArgs args)
    {
        await new ZapretDialog().ShowDialog((Window)this.GetVisualRoot()!);
    }

    private async void OpenNetworkDiagnostics(object? sender, RoutedEventArgs args)
    {
        await new NetworkDiagnosticsDialog().ShowDialog((Window)this.GetVisualRoot()!);
    }

    private async void OpenImport(object? sender, RoutedEventArgs args)
    {
        await new ImportDialog().ShowDialog((Window)this.GetVisualRoot()!);
    }

    private async void OpenHubSettings(object? sender, RoutedEventArgs args)
    {
        await new HubSettingsDialog().ShowDialog((Window)this.GetVisualRoot()!);
    }
}
