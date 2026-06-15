using System;
using System.ComponentModel;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Sonar.AutoSwitch.Pages;

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
            NavigationViewItem {Tag: "Home"} => typeof(Home),
            NavigationViewItem {Tag: "About"} => typeof(About),
            NavigationViewItem {Name: "SettingsItem"} => typeof(Settings),
            _ => throw new ArgumentOutOfRangeException()
        });
    }
}
