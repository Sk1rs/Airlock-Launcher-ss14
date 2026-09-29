using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SS14.Launcher.ViewModels;

namespace SS14.Launcher.Views;

public partial class ImportDialog : Window
{
    private readonly ImportViewModel _viewModel;

    public ImportDialog()
    {
        InitializeComponent();

        _viewModel = (ImportViewModel) DataContext!; // Set in XAML.
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        _viewModel.Populate();
    }

    private async void RunImport(object? sender, RoutedEventArgs args)
    {
        await _viewModel.RunImport();
    }

    private void CloseDialog(object? sender, RoutedEventArgs args) => Close();
}
