using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
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

        if (_exeBox != null)
        {
            _exeBox.ItemsSource = names;
            // Select-all via the template's inner TextBox so typing replaces the current value.
            // Guard against TemplateApplied re-fires (theme change) subscribing GotFocus twice.
            TextBox? innerTb = null;
            _exeBox.TemplateApplied += (_, e) =>
            {
                var tb = e.NameScope.Find<TextBox>("PART_TextBox");
                if (tb == null || tb == innerTb) return;
                innerTb = tb;
                innerTb.GotFocus += (_, _) => innerTb.SelectAll();
            };
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

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
