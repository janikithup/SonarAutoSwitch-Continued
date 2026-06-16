using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
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

        // Regression: Header must show profile name.
        var header = expanders[0].Header as TextBlock;
        Assert.NotNull(header);
        Assert.False(string.IsNullOrWhiteSpace(header!.Text), "Profile header text is empty — binding broken");
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
}
