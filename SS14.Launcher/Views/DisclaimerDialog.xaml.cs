using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SS14.Launcher.Localization;

namespace SS14.Launcher.Views;

/// <summary>
/// Credits shown before the launcher is usable: whose game this is, who forked it, what it is built on.
/// </summary>
/// <remarks>
/// The close button stays greyed out for a moment so the names are at least glanced at. The
/// delay is deliberately short: this is a courtesy to the people whose work is being used, not
/// an obstacle to playing.
/// </remarks>
public partial class DisclaimerDialog : Window
{
    private const string UpstreamUrl = "https://github.com/space-wizards/space-station-14";
    private const string ForkUrl = "https://github.com/Sk1rs";
    private const string BaseUrl = "https://github.com/Simple-Station/SimpleStationLauncher";

    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(2.42);

    private readonly DateTime _openedAt = DateTime.UtcNow;
    private readonly DispatcherTimer _timer;

    public DisclaimerDialog()
    {
        InitializeComponent();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Tick()
    {
        var left = Delay - (DateTime.UtcNow - _openedAt);
        var countdown = this.FindControl<TextBlock>("Countdown")!;

        if (left > TimeSpan.Zero)
        {
            countdown.Text = LocalizationManager.Instance.GetString(
                "disclaimer-wait",
                ("seconds", left.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));
            return;
        }

        countdown.Text = "";
        this.FindControl<Button>("CloseButton")!.IsEnabled = true;
        _timer.Stop();
    }

    /// <summary>
    /// Closing by other means (Alt+F4, the title bar) waits for the same delay as the button.
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DateTime.UtcNow - _openedAt < Delay)
            e.Cancel = true;

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }

    private void ClosePressed(object? sender, RoutedEventArgs args) => Close();

    private void OpenUpstream(object? sender, RoutedEventArgs args) => Helpers.OpenUri(new Uri(UpstreamUrl));

    private void OpenFork(object? sender, RoutedEventArgs args) => Helpers.OpenUri(new Uri(ForkUrl));

    private void OpenBase(object? sender, RoutedEventArgs args) => Helpers.OpenUri(new Uri(BaseUrl));
}
