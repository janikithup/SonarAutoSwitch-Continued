using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using Sonar.AutoSwitch.Services;

namespace Sonar.AutoSwitch.ViewModels;

public class HomeViewModel : ViewModelBase
{
    private ObservableCollection<AutoSwitchProfileViewModel> _autoSwitchProfiles =
        new() { new AutoSwitchProfileViewModel() };

    private SonarGamingConfiguration _defaultSonarGamingConfiguration = new(null, "unset");
    private SonarGamingConfiguration _activeProfile;
    // Search + sort: ephemeral view state, not persisted to JSON.
    private string _searchText = string.Empty;
    private int _sortDirection; // 0 = manual order, 1 = A→Z, -1 = Z→A

    public static IReadOnlyList<string> ProcessNames { get; } =
        Process.GetProcesses().Select(p => { var n = p.ProcessName; p.Dispose(); return n; }).Distinct().OrderBy(x => x).ToList();

    public HomeViewModel()
    {
        foreach (var p in _autoSwitchProfiles)
            Subscribe(p);
        _autoSwitchProfiles.CollectionChanged += AutoSwitchProfilesOnCollectionChanged;
    }

    public SonarGamingConfiguration DefaultSonarGamingConfiguration
    {
        get => _defaultSonarGamingConfiguration;
        set
        {
            if (Equals(value, _defaultSonarGamingConfiguration)) return;
            _defaultSonarGamingConfiguration = value;
            OnPropertyChanged(nameof(DefaultSonarGamingConfiguration));
        }
    }

    public ObservableCollection<AutoSwitchProfileViewModel> AutoSwitchProfiles
    {
        get => _autoSwitchProfiles;
        set
        {
            foreach (var p in _autoSwitchProfiles)
                p.PropertyChanged -= OnProfilePropertyChanged;
            _autoSwitchProfiles.CollectionChanged -= AutoSwitchProfilesOnCollectionChanged;

            _autoSwitchProfiles = value;

            foreach (var p in _autoSwitchProfiles)
                Subscribe(p);
            _autoSwitchProfiles.CollectionChanged += AutoSwitchProfilesOnCollectionChanged;
        }
    }

    [JsonIgnore]
    public SonarGamingConfiguration ActiveProfile
    {
        get => _activeProfile;
        set
        {
            if (Equals(value, _activeProfile)) return;
            _activeProfile = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            // base.OnPropertyChanged bypasses the override that calls SaveState.
            base.OnPropertyChanged(nameof(SearchText));
            base.OnPropertyChanged(nameof(FilteredProfiles));
        }
    }

    [JsonIgnore]
    public IEnumerable<AutoSwitchProfileViewModel> FilteredProfiles
    {
        get
        {
            IEnumerable<AutoSwitchProfileViewModel> source = _sortDirection switch
            {
                1  => _autoSwitchProfiles.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase),
                -1 => _autoSwitchProfiles.OrderByDescending(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase),
                2  => _autoSwitchProfiles.OrderByDescending(p => p.CreatedAt ?? DateTime.MinValue),
                -2 => _autoSwitchProfiles.OrderBy(p => p.CreatedAt ?? DateTime.MaxValue),
                _  => _autoSwitchProfiles
            };
            if (string.IsNullOrWhiteSpace(_searchText))
                return source is ObservableCollection<AutoSwitchProfileViewModel> ? source : source.ToList();
            var term = _searchText.Trim();
            return source.Where(p =>
                p.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.ExeName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Title.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    [JsonIgnore]
    public string SortModeLabel => _sortDirection switch
    {
        1  => "↑",
        -1 => "↓",
        2  => "⏰↓",
        -2 => "⏰↑",
        _  => "⇅",
    };

    [JsonIgnore]
    public string SortModeTooltip => _sortDirection switch
    {
        1  => "A to Z — click for Z to A",
        -1 => "Z to A — click for newest first",
        2  => "Newest first — click for oldest first",
        -2 => "Oldest first — click to unsort",
        _  => "Unsorted — click to sort A to Z",
    };

    public static HomeViewModel LoadHomeViewModel()
    {
        bool firstLoad = !StateManager.Instance.CheckStateExists<HomeViewModel>();
        var homeViewModel = StateManager.Instance.GetOrLoadState<HomeViewModel>();
        var steelSeriesSonarService = SteelSeriesSonarService.Instance;
        string selectedConfigId = steelSeriesSonarService.GetSelectedGamingConfiguration();
        var activeProfile = steelSeriesSonarService.GetGamingConfigurations()
            .FirstOrDefault(gc => gc.Id == selectedConfigId);
        if (firstLoad)
        {
            homeViewModel.DefaultSonarGamingConfiguration =
                activeProfile ?? homeViewModel.DefaultSonarGamingConfiguration;
        }

        homeViewModel.ActiveProfile = activeProfile ?? homeViewModel.DefaultSonarGamingConfiguration;

        // One-time backfill: stamp existing profiles that predate the CreatedAt field.
        // Use sequential dates so list order is preserved as recency order.
        var undated = homeViewModel._autoSwitchProfiles.Where(p => !p.CreatedAt.HasValue).ToList();
        if (undated.Count > 0)
        {
            var baseTime = DateTime.UtcNow.AddDays(-undated.Count);
            for (int i = 0; i < undated.Count; i++)
                undated[i].CreatedAt = baseTime.AddDays(i);
            StateManager.Instance.SaveState<HomeViewModel>();
        }

        return homeViewModel;
    }

    public void AddAutoSwitchProfile()
    {
        var profile = new AutoSwitchProfileViewModel { CreatedAt = DateTime.UtcNow };
        AutoSwitchProfiles.Add(profile); // Subscribe wired via CollectionChanged
        profile.IsExpanded = true;       // Accordion collapses others via OnProfilePropertyChanged
    }

    public void CycleSort()
    {
        // 0 (initial manual) goes to 1 on first click; never cycles back to 0 after that.
        _sortDirection = _sortDirection switch { 0 or -2 => 1, 1 => -1, -1 => 2, _ => -2 };
        base.OnPropertyChanged(nameof(FilteredProfiles));
        base.OnPropertyChanged(nameof(SortModeLabel));
        base.OnPropertyChanged(nameof(SortModeTooltip));
    }

    public void RemoveAutoSwitchProfile(AutoSwitchProfileViewModel profile)
    {
        AutoSwitchProfiles.Remove(profile);
        if (!AutoSwitchProfiles.Any())
        {
            var blank = new AutoSwitchProfileViewModel();
            AutoSwitchProfiles.Add(blank);
        }
    }

    private void Subscribe(AutoSwitchProfileViewModel profile)
    {
        profile.OnDeleteConfirmed = () => RemoveAutoSwitchProfile(profile);
        profile.PropertyChanged += OnProfilePropertyChanged;
    }

    private void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AutoSwitchProfileViewModel.IsExpanded))
        {
            if (sender is not AutoSwitchProfileViewModel profile) return;
            if (profile.IsExpanded)
                foreach (var p in AutoSwitchProfiles.Where(p => p != profile))
                    p.IsExpanded = false;
            return;
        }
        // Refresh filtered view when sort is active and a profile name changes.
        if (e.PropertyName is nameof(AutoSwitchProfileViewModel.ExeName)
                           or nameof(AutoSwitchProfileViewModel.Title))
        {
            if (_sortDirection != 0)
                base.OnPropertyChanged(nameof(FilteredProfiles));
        }
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName is nameof(FilteredProfiles)
                         or nameof(SortModeLabel)
                         or nameof(SortModeTooltip))
            return;
        StateManager.Instance.SaveState<HomeViewModel>();
    }

    private void AutoSwitchProfilesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        StateManager.Instance.SaveState<HomeViewModel>();
        base.OnPropertyChanged(nameof(FilteredProfiles));
        if (e.NewItems != null)
            foreach (AutoSwitchProfileViewModel p in e.NewItems)
                Subscribe(p);
        if (e.OldItems != null)
            foreach (AutoSwitchProfileViewModel p in e.OldItems)
                p.PropertyChanged -= OnProfilePropertyChanged;
    }
}
