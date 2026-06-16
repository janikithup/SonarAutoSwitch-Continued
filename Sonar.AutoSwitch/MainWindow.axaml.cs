using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Sonar.AutoSwitch.Pages;
using Sonar.AutoSwitch.Services;
using Sonar.AutoSwitch.Services.Win32;
using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch;

public partial class MainWindow : Window
{
    private readonly Frame _frameView;

    public MainWindow()
    {
        InitializeComponent();
        _frameView = this.FindControl<Frame>("FrameView")!;
        _frameView.Navigate(typeof(Home));
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        var settings = StateManager.Instance.GetOrLoadState<SettingsViewModel>();
        if (settings.CloseToTray)
        {
            if (!settings.HasShownTrayNotification)
            {
                settings.HasShownTrayNotification = true;
                StateManager.Instance.SaveStateNow<SettingsViewModel>();
                TrayBalloon.Show("Sonar Auto Switch",
                    "Still running in the background. Right-click the tray icon to exit.");
            }
            Hide();
            e.Cancel = true;
        }
        else
        {
            // ponytail: Post avoids re-entering OnClosing when Shutdown() closes this window.
            Dispatcher.UIThread.Post(() =>
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.Shutdown();
            });
        }
    }

    private void NavigationView_OnSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        _frameView.Navigate(e.SelectedItem switch
        {
            NavigationViewItem { Tag: "Home" } => typeof(Home),
            NavigationViewItem { Tag: "About" } => typeof(About),
            NavigationViewItem { Name: "SettingsItem" } => typeof(Settings),
            _ => throw new ArgumentOutOfRangeException()
        });
    }

    private void AddProfile_Click(object? sender, RoutedEventArgs e)
    {
        StateManager.Instance.GetOrLoadState<HomeViewModel>().AddAutoSwitchProfile();
        if (_frameView.CurrentSourcePageType != typeof(Home))
            _frameView.Navigate(typeof(Home));
        // Scroll after layout so the new (last) profile is visible
        Dispatcher.UIThread.Post(
            () => this.FindControl<ScrollViewer>("MainScrollViewer")?.ScrollToEnd(),
            DispatcherPriority.Loaded);
    }
}
