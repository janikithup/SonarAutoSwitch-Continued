using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Sonar.AutoSwitch.Pages;
using Sonar.AutoSwitch.Services;
using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch.Tests;

// The window title shows the active Sonar config and must follow it across switches.
// Drives real ActiveProfile changes (what AutoSwitchService does on a profile switch)
// against an injected VM, so the test exercises the live update without touching Sonar.
public class WindowTitleTest
{
    private static (Window, HomeViewModel) ShowHomeWith()
    {
        var home = new Home();
        var vm = new HomeViewModel();
        home.DataContext = vm;            // re-hooks the title logic to this VM
        var window = new Window { Width = 460, Height = 540, Content = home };
        window.Show();
        window.UpdateLayout();
        return (window, vm);
    }

    [AvaloniaFact]
    public void Title_updates_each_time_the_active_config_changes()
    {
        var (window, vm) = ShowHomeWith();

        vm.ActiveProfile = new SonarGamingConfiguration("id-1", "Competitive");
        window.UpdateLayout();
        Assert.Equal("Sonar Auto Switch · Competitive", window.Title);

        // A second switch must update again — the regression was that only the first sync worked.
        vm.ActiveProfile = new SonarGamingConfiguration("id-2", "Cinematic");
        window.UpdateLayout();
        Assert.Equal("Sonar Auto Switch · Cinematic", window.Title);
    }

    [AvaloniaFact]
    public void Title_falls_back_to_plain_name_when_no_active_config()
    {
        var (window, vm) = ShowHomeWith();

        vm.ActiveProfile = new SonarGamingConfiguration(null, "");
        window.UpdateLayout();
        Assert.Equal("Sonar Auto Switch", window.Title);
    }
}
