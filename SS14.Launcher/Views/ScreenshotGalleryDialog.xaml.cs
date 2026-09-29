using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using SS14.Launcher.ViewModels;

namespace SS14.Launcher.Views;

public partial class ScreenshotGalleryDialog : Window
{
    private readonly ScreenshotGalleryViewModel _viewModel;

    public ScreenshotGalleryDialog()
    {
        InitializeComponent();

        _viewModel = (ScreenshotGalleryViewModel) DataContext!; // Set in XAML.
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        _ = _viewModel.PopulateAsync();
    }

    /// <summary>
    /// Puts the file itself on the clipboard, so it can be pasted into chat as an attachment.
    /// </summary>
    private async void CopyPressed(object? sender, RoutedEventArgs args)
    {
        if (sender is not Control { DataContext: ScreenshotViewModel screenshot }
            || Clipboard is not { } clipboard
            || await StorageProvider.TryGetFileFromPathAsync(screenshot.Path) is not { } file)
        {
            return;
        }

        var data = new DataObject();
        data.Set(DataFormats.Files, new List<IStorageItem> { file });

        await clipboard.SetDataObjectAsync(data);
    }

    private void DeletePressed(object? sender, RoutedEventArgs args)
    {
        if (sender is Control { DataContext: ScreenshotViewModel screenshot })
            screenshot.Delete();
    }

    private void CloseDialog(object? sender, RoutedEventArgs args) => Close();
}
