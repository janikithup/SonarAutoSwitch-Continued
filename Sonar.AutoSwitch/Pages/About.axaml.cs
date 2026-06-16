using System.Reflection;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Sonar.AutoSwitch.Pages;

public partial class About : UserControl
{
    public About()
    {
        InitializeComponent();
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        this.FindControl<TextBlock>("VersionText")!.Text =
            v is null ? "Sonar Auto Switch" : $"Sonar Auto Switch v{v.Major}.{v.Minor}.{v.Build}";
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
