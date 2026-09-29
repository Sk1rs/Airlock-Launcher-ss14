using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SS14.Launcher.Models.Character;
using SS14.Launcher.ViewModels;

namespace SS14.Launcher.Views;

public partial class CharacterGalleryDialog : Window
{
    private readonly CharacterGalleryViewModel _viewModel;

    public CharacterGalleryDialog(CharacterGalleryViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();
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
    /// Loading a character is the point of the window, so it closes once one is picked.
    /// </summary>
    private void Loaded(object? sender, RoutedEventArgs args) => Close();

    private void OpenFolder(object? sender, RoutedEventArgs args) => Helpers.OpenFolder(CharacterLibrary.OurFolder);

    private void CloseDialog(object? sender, RoutedEventArgs args) => Close();
}
