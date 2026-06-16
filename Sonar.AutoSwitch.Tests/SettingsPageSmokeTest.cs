using System.Linq;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using Sonar.AutoSwitch.Pages;
using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch.Tests;

public class SettingsPageSmokeTest
{
    [AvaloniaFact]
    public void Settings_renders_four_expanders()
    {
        var window = new Window { Width = 600, Height = 500 };
        var settings = new Settings();
        settings.DataContext = new SettingsViewModel();
        window.Content = settings;
        window.Show();
        window.UpdateLayout();

        var expanders = settings.GetVisualDescendants().OfType<SettingsExpander>().ToList();
        Assert.Equal(4, expanders.Count);

        var headers = expanders.Select(e => e.Header?.ToString() ?? "").ToList();
        Assert.Contains("Enabled", headers);
        Assert.Contains("Start at startup", headers);
        Assert.Contains("Use GitHub profiles", headers);
        Assert.Contains("Close to tray", headers);
    }

    // B1 regression: JSON deserialization must not call RegisterInStartup or ToggleEnabled.
    // The [JsonConstructor] fix sets backing fields directly, bypassing property setters.
    [Fact]
    public void SettingsViewModel_deserializes_all_fields_correctly()
    {
        const string json = """{"Enabled":true,"StartAtStartup":false,"UseGithubConfigs":false,"CloseToTray":false}""";
        var vm = JsonSerializer.Deserialize<SettingsViewModel>(json);
        Assert.NotNull(vm);
        Assert.True(vm!.Enabled);
        Assert.False(vm.StartAtStartup);
        Assert.False(vm.UseGithubConfigs);
        Assert.False(vm.CloseToTray);
    }

    [AvaloniaFact]
    public void Settings_toggles_reflect_viewmodel_state()
    {
        var window = new Window { Width = 600, Height = 500 };
        var settings = new Settings();
        var vm = new SettingsViewModel();
        settings.DataContext = vm;
        window.Content = settings;
        window.Show();
        window.UpdateLayout();

        var toggles = settings.GetVisualDescendants().OfType<ToggleSwitch>().ToList();
        Assert.True(toggles.Count >= 4, "Expected at least 4 ToggleSwitches");

        // CloseToTray toggle should reflect default (true)
        var closeToTrayToggle = toggles.FirstOrDefault(t =>
            t.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "CloseToTray");
        Assert.NotNull(closeToTrayToggle);
        Assert.True(closeToTrayToggle!.IsChecked, "CloseToTray should default to true");
    }
}
