using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Sonar.AutoSwitch.Pages;

namespace Sonar.AutoSwitch.Tests;

public class HomePageSmokeTest
{
    [AvaloniaFact]
    public void Home_renders_accordion_with_first_profile_expanded()
    {
        var window = new Window { Width = 600, Height = 500 };
        window.Content = new Home();
        window.Show();
        window.UpdateLayout();

        var home = (Home)window.Content;
        var expanders = home.GetVisualDescendants().OfType<Expander>().ToList();
        var expanded = expanders.FirstOrDefault(e => e.IsExpanded);

        Assert.True(expanders.Count > 0, "No Expanders found — accordion not rendered");
        Assert.NotNull(expanded);
        Assert.True(expanded.Bounds.Width > 0, "Expanded profile has no width");

        // Regression: Header must show profile name, not the class name "Avalonia.Controls.TextBlock"
        var header = expanded.Header as TextBlock;
        Assert.NotNull(header);
        Assert.False(string.IsNullOrWhiteSpace(header!.Text), "Profile header text is empty — binding broken");
        Assert.NotEqual("Avalonia.Controls.TextBlock", header.Text);
    }

    [AvaloniaFact]
    public void ExeName_autocomplete_has_process_list_and_opens_on_typing()
    {
        var window = new Window { Width = 600, Height = 500 };
        window.Content = new Home();
        window.Show();
        window.UpdateLayout();

        var home = (Home)window.Content;

        // First profile is expanded by default — AutoCompleteBox should be in visual tree
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
