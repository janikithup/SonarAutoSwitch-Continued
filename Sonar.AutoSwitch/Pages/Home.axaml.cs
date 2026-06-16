using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch.Pages;

public partial class Home : UserControl
{
    private readonly AutoCompleteBox? _exeBox;
    private bool _syncing;

    public Home()
    {
        InitializeComponent();

        _exeBox = this.GetLogicalDescendants().OfType<AutoCompleteBox>().FirstOrDefault();
        var names = Process.GetProcesses().Select(p => p.ProcessName).Distinct().OrderBy(x => x).ToList();
        Log($"ExeNameBox={_exeBox != null}, ProcessNames={names.Count}");

        if (_exeBox != null)
        {
            _exeBox.ItemsSource = names;
            _exeBox.GotFocus += (_, _) =>
                _exeBox.GetVisualDescendants().OfType<TextBox>().FirstOrDefault()?.SelectAll();
        }

        DataContext = HomeViewModel.LoadHomeViewModel();

        if (_exeBox != null && DataContext is HomeViewModel vm)
        {
            vm.PropertyChanged += OnVmChanged;
            PushToBox(vm.SelectedAutoSwitchProfileViewModel?.ExeName);
            _exeBox.TextChanged += (_, _) =>
            {
                if (_syncing) return;
                if (DataContext is HomeViewModel h && h.SelectedAutoSwitchProfileViewModel is { } p)
                    p.ExeName = _exeBox.Text ?? "";
            };
        }
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.SelectedAutoSwitchProfileViewModel)
            && DataContext is HomeViewModel vm)
            PushToBox(vm.SelectedAutoSwitchProfileViewModel?.ExeName);
    }

    private void PushToBox(string? value)
    {
        if (_exeBox == null) return;
        _syncing = true;
        _exeBox.Text = value ?? "";
        _syncing = false;
    }

    private static void Log(string msg) =>
        File.AppendAllText(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Sonar.AutoSwitch", "debug.log"),
            $"{DateTime.Now:HH:mm:ss} [Home] {msg}\n");

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
