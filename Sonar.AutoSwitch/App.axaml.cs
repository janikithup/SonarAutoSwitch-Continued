using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Sonar.AutoSwitch.Services;
using Sonar.AutoSwitch.Services.Win32;
using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch;

public class App : Application
{
    public override void Initialize()
    {
        DataContext = new AppViewModel();
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var firstLoad = !StateManager.Instance.CheckStateExists<SettingsViewModel>();
        var settingsViewModel = StateManager.Instance.GetOrLoadState<SettingsViewModel>();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            // ponytail: Avalonia auto-shows desktop.MainWindow in Start(). Only create it when
            // it should be visible; Open() creates it lazily via MainWindow ??= new MainWindow().
            if (firstLoad || (desktop.Args ?? []).Contains("--show"))
            {
                desktop.MainWindow = new MainWindow();
                desktop.MainWindow.Show();
            }
            if (firstLoad)
                StateManager.Instance.SaveState<SettingsViewModel>();
        }

        var homeViewModel = StateManager.Instance.GetOrLoadState<HomeViewModel>();
        var appVm = (AppViewModel)DataContext!;
        appVm.WireTooltip(homeViewModel, settingsViewModel);

        if (settingsViewModel.Enabled)
            AutoSwitchService.Instance.ToggleEnabled(settingsViewModel.Enabled);
        if (firstLoad && settingsViewModel.StartAtStartup)
            StartupService.RegisterInStartup(true);

        _ = AutoSwitchProfilesDatabase.Instance.LoadDatabaseAsync();
        base.OnFrameworkInitializationCompleted();
    }
}