using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Sonar.AutoSwitch.Pages;
using Sonar.AutoSwitch.Services;
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
        Hide();
        e.Cancel = true;
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
