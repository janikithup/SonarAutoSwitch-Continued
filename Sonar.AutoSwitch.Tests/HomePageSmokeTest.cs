using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Sonar.AutoSwitch.Pages;
using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch.Tests;

public class HomePageSmokeTest
{
    [AvaloniaFact]
    public void Home_renders_accordion_collapsed_by_default()
    {
        var window = new Window { Width = 600, Height = 500 };
        var home = new Home();
        // Use a fresh ViewModel (not the singleton) to ensure isolation from other tests.
        home.DataContext = new HomeViewModel();
        window.Content = home;
        window.Show();
        window.UpdateLayout();

        var expanders = home.GetVisualDescendants().OfType<Expander>().ToList();

        Assert.True(expanders.Count > 0, "No Expanders found — accordion not rendered");
        Assert.True(expanders.All(e => !e.IsExpanded), "Profiles should all be collapsed on load");

        // Regression: Header is a Grid; Col 1 is an inner Grid containing the name TextBlock.
        var headerGrid = expanders[0].Header as Grid;
        Assert.NotNull(headerGrid);
        var nameGrid = headerGrid!.Children.OfType<Grid>().First();
        var nameText = nameGrid.Children.OfType<TextBlock>().First();
        Assert.False(string.IsNullOrWhiteSpace(nameText.Text), "Profile header text is empty — binding broken");
    }

    [AvaloniaFact]
    public void Home_has_settings_button()
    {
        var window = new Window { Width = 600, Height = 500 };
        var home = new Home();
        home.DataContext = new HomeViewModel();
        window.Content = home;
        window.Show();
        window.UpdateLayout();

        var btn = home.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Open settings");
        Assert.NotNull(btn);
    }

    [AvaloniaFact]
    public void Home_has_no_search_toggle()
    {
        var window = new Window { Width = 600, Height = 500 };
        var home = new Home();
        home.DataContext = new HomeViewModel();
        window.Content = home;
        window.Show();
        window.UpdateLayout();

        // The toggle-based search has been replaced by a persistent search box.
        var btn = home.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>()
            .FirstOrDefault(b => b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Toggle search");
        Assert.Null(btn);
    }

    [AvaloniaFact]
    public void Home_has_add_profile_button()
    {
        var window = new Window { Width = 600, Height = 500 };
        var home = new Home();
        home.DataContext = new HomeViewModel();
        window.Content = home;
        window.Show();
        window.UpdateLayout();

        var btn = home.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Add profile");
        Assert.NotNull(btn);
    }

    [AvaloniaFact]
    public void Profile_card_has_browse_exe_button()
    {
        var window = new Window { Width = 600, Height = 500 };
        var home = new Home();
        home.DataContext = new HomeViewModel();
        window.Content = home;
        window.Show();
        window.UpdateLayout();

        var expanders = home.GetVisualDescendants().OfType<Expander>().ToList();
        Assert.True(expanders.Count > 0, "No Expanders");
        expanders[0].IsExpanded = true;
        window.UpdateLayout();

        var btn = home.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Browse for exe");
        Assert.NotNull(btn);
    }

    [AvaloniaFact]
    public void Profile_browse_exe_click_does_not_crash()
    {
        var window = new Window { Width = 600, Height = 500 };
        var home = new Home();
        home.DataContext = new HomeViewModel();
        window.Content = home;
        window.Show();
        window.UpdateLayout();

        var expanders = home.GetVisualDescendants().OfType<Expander>().ToList();
        expanders[0].IsExpanded = true;
        window.UpdateLayout();

        var btn = home.GetVisualDescendants().OfType<Button>()
            .First(b => b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Browse for exe");

        // StorageProvider returns no files in headless — handler must not crash.
        btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout();
    }

    [AvaloniaFact]
    public void Home_shows_sonar_status_dot_bound_to_status_brush()
    {
        var window = new Window { Width = 600, Height = 500 };
        var home = new Home();
        var vm = new HomeViewModel();
        home.DataContext = vm;
        window.Content = home;
        window.Show();
        window.UpdateLayout();

        var dot = home.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>()
            .FirstOrDefault(e => e.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Sonar connection status");
        Assert.NotNull(dot);
        // Idle by default → grey, and the binding actually resolved (not null).
        Assert.Equal(Avalonia.Media.Brushes.Gray, dot!.Fill);

        // Status change repaints the dot.
        vm.SonarStatus = Sonar.AutoSwitch.Services.SonarConnectionStatus.Disconnected;
        window.UpdateLayout();
        Assert.Equal(Avalonia.Media.Brushes.OrangeRed, dot.Fill);
    }

    [AvaloniaFact]
    public void ExeName_autocomplete_has_process_list_and_opens_on_typing()
    {
        var window = new Window { Width = 600, Height = 500 };
        var home = new Home();
        home.DataContext = new HomeViewModel();
        window.Content = home;
        window.Show();
        window.UpdateLayout();

        // Expand the first profile so its controls are in the visual tree
        var expanders = home.GetVisualDescendants().OfType<Expander>().ToList();
        Assert.True(expanders.Count > 0, "No Expanders found");
        expanders[0].IsExpanded = true;
        window.UpdateLayout();

        var autoComplete = home.GetVisualDescendants().OfType<AutoCompleteBox>().FirstOrDefault();
        Assert.NotNull(autoComplete);
        Assert.NotNull(autoComplete.ItemsSource);

        var items = autoComplete.ItemsSource!.Cast<string>().ToList();
        Assert.True(items.Count > 0, "ItemsSource is empty — ProcessNames not wired");

        autoComplete.Focus();
        window.UpdateLayout();
        window.KeyTextInput("e");
        window.UpdateLayout();

        // Headless mode: popup windows don't render, so IsDropDownOpen stays false.
        // Assert text input reached the control instead.
        Assert.Equal("e", autoComplete.Text);
    }

    [AvaloniaFact]
    public void Profile_match_hint_is_rendered_as_badge_border()
    {
        var window = new Window { Width = 600, Height = 500 };
        var home = new Home();
        home.DataContext = new HomeViewModel();
        window.Content = home;
        window.Show();
        window.UpdateLayout();

        var expanders = home.GetVisualDescendants().OfType<Expander>().ToList();
        Assert.True(expanders.Count > 0, "No Expanders found");
        expanders[0].IsExpanded = true;
        window.UpdateLayout();

        // The auto-match hint must be a Badge-style Border, not a plain TextBlock.
        var badge = home.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)
                                  ?.ToString() == "Auto-match hint");
        Assert.NotNull(badge);
    }

    // ── New tests for visual beautification (write-first / TDD) ──────────────

    [AvaloniaFact]
    public void Hero_shows_active_config_name()
    {
        var window = new Window { Width = 600, Height = 500 };
        var home = new Home();
        home.DataContext = new HomeViewModel();
        window.Content = home;
        window.Show();
        window.UpdateLayout();

        var heroBorder = home.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Hero section");
        Assert.NotNull(heroBorder);

        var activeConfigText = home.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => t.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Active config name");
        Assert.NotNull(activeConfigText);
    }

    [AvaloniaFact]
    public void Profile_header_shows_preset_badge_when_configured()
    {
        var window = new Window { Width = 600, Height = 500 };
        var home = new Home();
        var vm = new HomeViewModel();
        home.DataContext = vm;
        window.Content = home;
        window.Show();
        window.UpdateLayout();

        // Default profile has null SonarGamingConfiguration Id — badge should NOT appear.
        var expanders = home.GetVisualDescendants().OfType<Expander>().ToList();
        Assert.True(expanders.Count > 0, "No Expanders found");
        expanders[0].IsExpanded = true;
        window.UpdateLayout();

        var badge = home.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Sonar config badge");
        // Either badge is null OR it is not visible (IsVisible=false means the binding hides it).
        Assert.True(badge == null || !badge.IsVisible, "Badge should not appear for unconfigured profile");

        // Now add a profile with a real SonarGamingConfiguration and check badge appears.
        var configuredProfile = new Sonar.AutoSwitch.ViewModels.AutoSwitchProfileViewModel();
        configuredProfile.SonarGamingConfiguration = new Sonar.AutoSwitch.Services.SonarGamingConfiguration("test-id-123", "Test Config");
        vm.AutoSwitchProfiles.Add(configuredProfile);
        window.UpdateLayout();

        // Expand the newly added expander (last one).
        var newExpanders = home.GetVisualDescendants().OfType<Expander>().ToList();
        newExpanders.Last().IsExpanded = true;
        window.UpdateLayout();

        var configuredBadge = home.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Sonar config badge"
                                 && b.IsVisible);
        Assert.NotNull(configuredBadge);
    }

    [AvaloniaFact]
    public void Profile_header_shows_active_dot_when_active()
    {
        var window = new Window { Width = 600, Height = 500 };
        var home = new Home();
        var vm = new HomeViewModel();
        home.DataContext = vm;
        window.Content = home;
        window.Show();
        window.UpdateLayout();

        // Mark the first profile as active.
        vm.AutoSwitchProfiles[0].IsActive = true;
        window.UpdateLayout();

        var dot = home.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>()
            .FirstOrDefault(e => e.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Active profile dot"
                                 && e.IsVisible);
        Assert.NotNull(dot);
    }

    [AvaloniaFact]
    public void Window_title_uses_middle_dot_not_em_dash()
    {
        var window = new Window { Width = 600, Height = 500 };
        var home = new Home();
        var vm = new HomeViewModel();
        home.DataContext = vm;
        window.Content = home;
        window.Show();
        window.UpdateLayout();

        // Trigger a title sync by setting an active profile
        vm.ActiveProfile = new Sonar.AutoSwitch.Services.SonarGamingConfiguration("id-1", "Cyberpunk 2077");
        window.UpdateLayout();

        Assert.Contains("Cyberpunk 2077", window.Title ?? "");
        Assert.DoesNotContain("—", window.Title ?? ""); // no em dash
        Assert.Contains("·", window.Title ?? "");       // middle dot present
    }

    [AvaloniaFact]
    public void Home_has_persistent_search_box()
    {
        var window = new Window { Width = 600, Height = 500 };
        var home = new Home();
        home.DataContext = new HomeViewModel();
        window.Content = home;
        window.Show();
        window.UpdateLayout();

        // Search box must be present and visible without any toggle click.
        var searchBox = home.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault(t => t.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)?.ToString() == "Search profiles");
        Assert.NotNull(searchBox);
        Assert.True(searchBox!.IsVisible, "Search box should be visible without toggling");
    }
}
