using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SS14.Launcher.Views.MainWindowTabs;

public partial class StatsTabView : UserControl
{
    public StatsTabView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
