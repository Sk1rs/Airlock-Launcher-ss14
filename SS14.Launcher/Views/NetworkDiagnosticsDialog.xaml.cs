using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SS14.Launcher.ViewModels;

namespace SS14.Launcher.Views;

public partial class NetworkDiagnosticsDialog : Window
{
    private readonly NetworkDiagnosticsViewModel _viewModel;

    public NetworkDiagnosticsDialog()
    {
        InitializeComponent();

        _viewModel = (NetworkDiagnosticsViewModel) DataContext!; // Set in XAML.
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Nobody opens this window to look at an empty table.
        _ = _viewModel.RunAsync();
    }

    private async void RunAgain(object? sender, RoutedEventArgs args)
    {
        await _viewModel.RunAsync();
    }

    private void CloseDialog(object? sender, RoutedEventArgs args) => Close();
}
