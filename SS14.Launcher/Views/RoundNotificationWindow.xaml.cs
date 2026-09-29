using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SS14.Launcher.Localization;
using SS14.Launcher.Models;

namespace SS14.Launcher.Views;

/// <summary>
/// A small card in the corner of the screen saying a watched server is worth joining now.
/// </summary>
/// <remarks>
/// It sits on top of other windows but never steals focus, and goes away on its own, so a
/// player alt-tabbed into something else sees it without being yanked out of what they were doing.
/// </remarks>
public partial class RoundNotificationWindow : Window
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(25);

    private readonly Action _connect;
    private readonly DispatcherTimer _timer;

    public RoundNotificationWindow(RoundNotification notification, Action connect)
    {
        _connect = connect;

        InitializeComponent();

        var loc = LocalizationManager.Instance;

        this.FindControl<TextBlock>("Headline")!.Text = notification.ServerName;
        this.FindControl<TextBlock>("Body")!.Text = notification.Event switch
        {
            RoundEvent.LobbyOpened => loc.GetString("watch-notification-lobby", ("players", notification.Players)),
            _ => loc.GetString("watch-notification-players", ("players", notification.Players)),
        };

        _timer = new DispatcherTimer { Interval = Lifetime };
        _timer.Tick += (_, _) => Close();
        _timer.Start();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Bottom-right of whichever screen holds the launcher, clear of the taskbar.
        if (Screens.Primary is { } screen)
        {
            var area = screen.WorkingArea;
            var scale = screen.Scaling;

            Position = new PixelPoint(
                (int) (area.Right - Width * scale - 16 * scale),
                (int) (area.Bottom - Height * scale - 16 * scale));
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }

    private void ConnectPressed(object? sender, RoutedEventArgs args)
    {
        Close();
        _connect();
    }

    private void DismissPressed(object? sender, RoutedEventArgs args) => Close();
}
