using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Sonar.AutoSwitch.Pages;

namespace Sonar.AutoSwitch.Tests;

public class HomePageSmokeTest
{
    [AvaloniaFact]
    public void Home_xaml_loads_without_throwing()
    {
        // Compiled binding failures throw during InitializeComponent, before any rendering.
        // Not calling Show() — font rendering in headless mode is unrelated to what we're testing.
        var home = new Home();
        Assert.NotNull(home);
    }
}
