using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch.Tests;

public class AppViewModelTest
{
    [Fact]
    public void TrayTooltip_shows_profile_count_when_enabled()
    {
        var home = new HomeViewModel();
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel());
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel());
        // default has 1 profile; we added 2 more → 3 total
        var settings = new SettingsViewModel { Enabled = true };
        var vm = new AppViewModel();
        vm.WireTooltip(home, settings);

        Assert.Contains("3", vm.TrayTooltipText);
        Assert.DoesNotContain("disabled", vm.TrayTooltipText);
    }

    [Fact]
    public void TrayTooltip_shows_disabled_when_service_off()
    {
        var home = new HomeViewModel();
        var settings = new SettingsViewModel { Enabled = false };
        var vm = new AppViewModel();
        vm.WireTooltip(home, settings);

        Assert.Contains("disabled", vm.TrayTooltipText);
    }

    [Fact]
    public void TrayTooltip_updates_when_profile_added()
    {
        var home = new HomeViewModel();
        var settings = new SettingsViewModel { Enabled = true };
        var vm = new AppViewModel();
        vm.WireTooltip(home, settings);
        var before = vm.TrayTooltipText;

        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel());

        Assert.NotEqual(before, vm.TrayTooltipText);
    }

    [Fact]
    public void TrayTooltip_updates_when_enabled_toggled()
    {
        var home = new HomeViewModel();
        var settings = new SettingsViewModel { Enabled = true };
        var vm = new AppViewModel();
        vm.WireTooltip(home, settings);

        settings.Enabled = false;

        Assert.Contains("disabled", vm.TrayTooltipText);
    }

    [Fact]
    public void StartupDescription_returns_string()
    {
        var vm = new SettingsViewModel();
        Assert.NotNull(vm.StartupDescription);
        Assert.NotEqual(string.Empty, vm.StartupDescription);
    }
}
