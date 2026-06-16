using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Sonar.AutoSwitch.ViewModels;

public class AppViewModel : ViewModelBase
{
    private string _trayTooltipText = "Sonar Auto Switch";

    public string TrayTooltipText
    {
        get => _trayTooltipText;
        private set => SetField(ref _trayTooltipText, value);
    }

    public void WireTooltip(HomeViewModel home, SettingsViewModel settings)
    {
        UpdateTooltip(home, settings);
        home.AutoSwitchProfiles.CollectionChanged += (_, _) => UpdateTooltip(home, settings);
        // ponytail: override strips [CallerMemberName]; null = "all changed" in Avalonia.
        settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null or nameof(SettingsViewModel.Enabled))
                UpdateTooltip(home, settings);
        };
    }

    private void UpdateTooltip(HomeViewModel home, SettingsViewModel settings)
    {
        int n = home.AutoSwitchProfiles.Count;
        TrayTooltipText = settings.Enabled
            ? $"Sonar Auto Switch — {n} profile{(n == 1 ? "" : "s")}"
            : "Sonar Auto Switch — disabled";
    }

    public void Open()
    {
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.MainWindow ??= new MainWindow();
            lifetime.MainWindow.Show();
        }
    }

    public void Exit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            lifetime.Shutdown();
    }
}