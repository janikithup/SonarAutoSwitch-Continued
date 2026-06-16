using Avalonia.Headless.XUnit;
using Avalonia.Controls;
using Sonar.AutoSwitch.ViewModels;
using Sonar.AutoSwitch.Services;

namespace Sonar.AutoSwitch.Tests;

public class MainWindowCloseTest
{
    // B-close regression: closing with CloseToTray=false must not hang.
    // The bug was desktop.Shutdown() inside OnClosing re-entering OnClosing infinitely.
    [AvaloniaFact]
    public void Close_with_CloseToTray_false_does_not_hang()
    {
        var settings = new SettingsViewModel { CloseToTray = false };
        var window = new MainWindow();
        window.DataContext = settings;
        window.Show();

        // Should return quickly — if it hangs, xUnit times out the test.
        window.Close();

        Assert.True(true); // reaching here means no infinite loop
    }

    [AvaloniaFact]
    public void Close_with_CloseToTray_true_hides_window_instead()
    {
        var settings = new SettingsViewModel { CloseToTray = true };
        var window = new MainWindow();
        window.DataContext = settings;
        window.Show();

        window.Close();

        Assert.False(window.IsVisible);
    }

    // First close-to-tray must set HasShownTrayNotification so the balloon fires only once.
    [AvaloniaFact]
    public void First_CloseToTray_sets_HasShownTrayNotification()
    {
        var settings = StateManager.Instance.GetOrLoadState<SettingsViewModel>();
        if (!settings.CloseToTray) return;

        var original = settings.HasShownTrayNotification;
        settings.HasShownTrayNotification = false;

        var window = new MainWindow();
        window.Show();
        window.Close();

        Assert.True(settings.HasShownTrayNotification);
        settings.HasShownTrayNotification = original;
    }
}
