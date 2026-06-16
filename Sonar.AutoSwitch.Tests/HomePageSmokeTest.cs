using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Sonar.AutoSwitch.Pages;

namespace Sonar.AutoSwitch.Tests;

public class HomePageSmokeTest
{
    [AvaloniaFact]
    public void Home_renders_profile_list_and_exe_field()
    {
        var window = new Window { Width = 600, Height = 450 };
        window.Content = new Home();
        window.Show();
        window.UpdateLayout();

        var home = (Home)window.Content;
        var listBox = home.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
        var autoComplete = home.GetVisualDescendants().OfType<AutoCompleteBox>().FirstOrDefault();

        Assert.NotNull(listBox);
        Assert.NotNull(autoComplete);
        Assert.True(listBox.Bounds.Width > 0, "Profile list not rendered");
        Assert.True(autoComplete.Bounds.Width > 0, "ExeName field not rendered");
    }

    [AvaloniaFact]
    public void ExeName_autocomplete_shows_dropdown_when_typing()
    {
        var window = new Window { Width = 600, Height = 450 };
        window.Content = new Home();
        window.Show();
        window.UpdateLayout();

        var home = (Home)window.Content;
        var autoComplete = home.GetVisualDescendants().OfType<AutoCompleteBox>().First();

        // Focus the field, clear it, type a prefix
        autoComplete.Focus();
        window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control); // select all
        window.KeyTextInput("f");
        window.UpdateLayout();

        // Dropdown should be open with at least one item (firefox, etc. will be running)
        Assert.True(autoComplete.IsDropDownOpen, "Dropdown did not open after typing");
    }
}
