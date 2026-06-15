using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Sonar.AutoSwitch.Pages;

namespace Sonar.AutoSwitch.Tests;

public class HomePageSmokeTest
{
    [AvaloniaFact]
    public void Home_xaml_loads_without_throwing()
    {
        // Catches compiled binding failures (wrong property name/type on ViewModel).
        // FluentAvalonia symbol fonts fail in headless rendering — Show() is not called.
        // Visual verification requires screenshotting the running app.
        var home = new Home();
        Assert.NotNull(home);
    }
}
