using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch.Tests;

// Regression: UseGithubConfigs was an auto-property — toggling it never raised
// PropertyChanged, so SaveStateNow was never called and the value was never persisted.
public class SettingsViewModelTest
{
    [Fact]
    public void UseGithubConfigs_raises_PropertyChanged_when_toggled()
    {
        var vm = new SettingsViewModel();
        var raised = false;
        vm.PropertyChanged += (_, _) => { raised = true; };
        vm.UseGithubConfigs = !vm.UseGithubConfigs;
        Assert.True(raised);
    }

    [Fact]
    public void CloseToTray_raises_PropertyChanged_when_toggled()
    {
        var vm = new SettingsViewModel();
        var raised = false;
        vm.PropertyChanged += (_, _) => { raised = true; };
        vm.CloseToTray = !vm.CloseToTray;
        Assert.True(raised);
    }
}
