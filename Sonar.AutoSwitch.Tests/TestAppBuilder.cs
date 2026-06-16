using Avalonia;
using Avalonia.Headless;
using FluentAvalonia.Styling;
using Sonar.AutoSwitch.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Sonar.AutoSwitch.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .UseSkia()
            .AfterSetup(_ =>
            {
                Application.Current!.Styles.Add(new FluentAvaloniaTheme());
            });
}
