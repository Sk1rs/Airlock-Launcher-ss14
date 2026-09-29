using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Linq;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using SS14.Launcher.ViewModels;

namespace SS14.Launcher.Views;

public partial class ZapretDialog : Window
{
    private readonly ZapretViewModel _viewModel;

    public ZapretDialog()
    {
        InitializeComponent();

        _viewModel = (ZapretViewModel) DataContext!; // Set in XAML.
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Someone may have started or stopped it outside the launcher.
        _viewModel.Refresh();
    }

    private async void InstallPressed(object? sender, RoutedEventArgs args)
    {
        await _viewModel.InstallAsync();
    }

    private async void InstallFromFilePressed(object? sender, RoutedEventArgs args)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("zapret") { Patterns = ["*.zip"] }],
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
            _viewModel.InstallFromArchive(path);
    }

    private void CloseDialog(object? sender, RoutedEventArgs args) => Close();
}
