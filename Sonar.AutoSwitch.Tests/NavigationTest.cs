using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Sonar.AutoSwitch.Pages;

namespace Sonar.AutoSwitch.Tests;

public class NavigationTest
{
    [AvaloniaFact]
    public void OpenSettings_click_swaps_content_to_settings_page()
    {
        var window = new MainWindow();
        window.Show();
        window.UpdateLayout();

        var pageHost = window.FindControl<ContentControl>("PageHost")!;
        Assert.IsType<Home>(pageHost.Content);

        var home = (Home)pageHost.Content;
        var btn = home.GetVisualDescendants().OfType<Button>()
            .First(b => b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Open settings");
        btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout();

        Assert.IsType<Settings>(pageHost.Content);
    }

    [AvaloniaFact]
    public void Back_click_returns_content_to_home_page()
    {
        var window = new MainWindow();
        window.Show();
        window.UpdateLayout();

        window.ShowSettings();
        window.UpdateLayout();

        var pageHost = window.FindControl<ContentControl>("PageHost")!;
        var settings = (Settings)pageHost.Content!;
        var btn = settings.GetVisualDescendants().OfType<Button>()
            .First(b => b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Back to profiles");
        btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout();

        Assert.IsType<Home>(pageHost.Content);
    }
}
