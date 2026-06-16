using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Interactivity;
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

    private static (Window, Settings) CreateWindow()
    {
        var w = new Window { Width = 600, Height = 500 };
        var s = new Settings();
        w.Content = s;
        w.Show();
        w.UpdateLayout();
        return (w, s);
    }

    [AvaloniaFact]
    public void Settings_check_updates_click_does_not_crash_and_shows_status()
    {
        var (window, settings) = CreateWindow();

        var updateStatus = settings.FindControl<TextBlock>("UpdateStatus")!;
        Assert.NotNull(updateStatus);
        Assert.True(string.IsNullOrEmpty(updateStatus.Text), "UpdateStatus should start empty");

        // Click "Check for updates" — network will fail in test env, handler must catch and set status.
        var btn = settings.GetVisualDescendants().OfType<Button>()
            .First(b => b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Check for updates");
        btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout();

        // Immediately after click: "Checking..." (the await hasn't resolved yet).
        Assert.Equal("Checking...", updateStatus.Text);
    }

    [AvaloniaFact]
    public void Settings_reset_confirm_panel_hidden_until_reset_clicked()
    {
        var (_, settings) = CreateWindow();

        var confirmPanel = settings.FindControl<StackPanel>("ResetConfirmPanel")!;
        Assert.NotNull(confirmPanel);
        Assert.False(confirmPanel.IsVisible, "Confirm panel should be hidden initially");

        var resetBtn = settings.GetVisualDescendants().OfType<Button>()
            .First(b => b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Reset to defaults");
        resetBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        settings.UpdateLayout();

        Assert.True(confirmPanel.IsVisible, "Confirm panel should show after Reset clicked");
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

    [AvaloniaFact]
    public void Settings_open_log_click_shows_no_log_message_when_file_absent()
    {
        // Move the real log file away so the handler takes the "no file" branch
        // and never calls Process.Start (which would open Notepad during tests).
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sonar.AutoSwitch", "debug.log");
        var temp = logPath + ".test_hide";
        bool moved = File.Exists(logPath);
        if (moved) File.Move(logPath, temp);
        try
        {
            var (_, settings) = CreateWindow();
            var updateStatus = settings.FindControl<TextBlock>("UpdateStatus")!;
            var btn = settings.GetVisualDescendants().OfType<Button>()
                .First(b => b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Open log file");
            btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            settings.UpdateLayout();
            Assert.Equal("No log file yet.", updateStatus.Text);
        }
        finally
        {
            if (moved) File.Move(temp, logPath, overwrite: true);
        }
    }
}
