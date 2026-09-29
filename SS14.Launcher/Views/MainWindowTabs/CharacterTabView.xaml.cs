using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using SS14.Launcher.ViewModels;
using SS14.Launcher.ViewModels.MainWindowTabs;

namespace SS14.Launcher.Views.MainWindowTabs;

public partial class CharacterTabView : UserControl
{
    public CharacterTabView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void MarkingHovered(object? sender, PointerEventArgs args)
    {
        if (sender is Control { DataContext: MarkingOptionViewModel option }
            && DataContext is CharacterTabViewModel vm)
        {
            vm.PreviewMarking(option);
        }
    }

    private void MarkingDropDownClosed(object? sender, EventArgs args)
    {
        (DataContext as CharacterTabViewModel)?.ClearPreview();
    }

    /// <summary>
    /// Puts the drawn character on the clipboard as a file, ready to paste into a chat.
    /// </summary>
    private async void CopyPressed(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not CharacterTabViewModel vm
            || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        if (vm.WriteToTemp() is not { } path
            || await TopLevel.GetTopLevel(this)!.StorageProvider.TryGetFileFromPathAsync(path) is not { } file)
        {
            return;
        }

        var data = new DataObject();
        data.Set(DataFormats.Files, new List<IStorageItem> { file });

        await clipboard.SetDataObjectAsync(data);
    }

    private async void GalleryPressed(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not CharacterTabViewModel vm || TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var gallery = new CharacterGalleryViewModel(vm.RenderProfileAsync, vm.LoadProfile);
        await new CharacterGalleryDialog(gallery).ShowDialog(owner);
    }

    private async void ImportProfilePressed(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not CharacterTabViewModel vm)
            return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null)
            return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            SuggestedStartLocation = await CharactersFolderAsync(storage),
            FileTypeFilter = [YamlFileType],
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
            vm.ImportProfile(path);
    }

    /// <summary>
    /// Opens the pickers where the game keeps its characters, when that folder exists yet.
    /// </summary>
    private static async Task<IStorageFolder?> CharactersFolderAsync(IStorageProvider storage)
    {
        try
        {
            var path = CharacterTabViewModel.CharactersDirectory;

            return Directory.Exists(path) ? await storage.TryGetFolderFromPathAsync(path) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static readonly FilePickerFileType YamlFileType =
        new("Character") { Patterns = ["*.yml", "*.yaml"] };

    private async void ExportPressed(object? sender, RoutedEventArgs args)
    {
        if (DataContext is not CharacterTabViewModel vm || !vm.CanExport)
            return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null)
            return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = vm.SuggestedFileName,
            DefaultExtension = "png",
            FileTypeChoices = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }],
        });

        if (file?.TryGetLocalPath() is not { } path)
            return;

        vm.Export(path);
    }
}
