using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Avalonia.Media;
using Avalonia.Threading;
using Sonar.AutoSwitch.Services;

namespace Sonar.AutoSwitch.ViewModels;

public class HomeViewModel : ViewModelBase
{
    private ObservableCollection<AutoSwitchProfileViewModel> _autoSwitchProfiles =
        new() { new AutoSwitchProfileViewModel() };

    private SonarGamingConfiguration _defaultSonarGamingConfiguration = new(null, "unset");
    private SonarGamingConfiguration _activeProfile;
    private SonarConnectionStatus _sonarStatus = SonarConnectionStatus.Idle;
    // Search + sort: ephemeral view state, not persisted to JSON.
    private string _searchText = string.Empty;
    private int _sortDirection; // 0 = manual order, 1 = A→Z, -1 = Z→A
    // Audio peak — ephemeral, drives EQ bar heights.
    private double _audioPeak;

    public static IReadOnlyList<string> ProcessNames { get; } =
        Process.GetProcesses()
            .Select(p =>
            {
                try
                {
                    var path = p.MainModule?.FileName;
                    var name = p.ProcessName;
                    p.Dispose();
                    // Filter system processes: null path or path can't be read → system process.
                    return path != null && !RecentWindowsService.IsSystemExePath(path) ? name : null;
                }
                catch { p.Dispose(); return null; }
            })
            .OfType<string>()
            .Distinct()
            .OrderBy(x => x)
            .ToList();

    public HomeViewModel()
    {
        foreach (var p in _autoSwitchProfiles)
            Subscribe(p);
        _autoSwitchProfiles.CollectionChanged += AutoSwitchProfilesOnCollectionChanged;
        AudioMeterService.Instance.PeakChanged += OnAudioPeak;
    }

    private void OnAudioPeak(object? sender, float peak)
    {
        _audioPeak = (double)peak;
        Dispatcher.UIThread.Post(() =>
        {
            base.OnPropertyChanged(nameof(EqBar1));
            base.OnPropertyChanged(nameof(EqBar2));
            base.OnPropertyChanged(nameof(EqBar3));
            base.OnPropertyChanged(nameof(EqBar4));
            base.OnPropertyChanged(nameof(EqBar5));
            base.OnPropertyChanged(nameof(EqBar6));
        });
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

    // Set only by --demo: a fully-formed in-memory state that must not be touched by real Sonar reads.
    [JsonIgnore]
    public bool IsDemo { get; set; }

    [JsonIgnore]
    public SonarGamingConfiguration ActiveProfile
    {
        get => _activeProfile;
        set
        {
            if (Equals(value, _activeProfile)) return;
            _activeProfile = value;
            // Update IsActive on all profiles to reflect the newly active config.
            foreach (var p in _autoSwitchProfiles)
                p.IsActive = p.SonarGamingConfiguration.Id != null
                             && p.SonarGamingConfiguration.Id == value?.Id;
            OnPropertyChanged();
        }
    }

    // Connection status to Sonar, driven by AutoSwitchService after each switch attempt.
    // Ephemeral: uses base.OnPropertyChanged to skip the SaveState path.
    [JsonIgnore]
    public SonarConnectionStatus SonarStatus
    {
        get => _sonarStatus;
        set
        {
            if (_sonarStatus == value) return;
            _sonarStatus = value;
            base.OnPropertyChanged(nameof(SonarStatus));
            base.OnPropertyChanged(nameof(SonarStatusBrush));
            base.OnPropertyChanged(nameof(SonarStatusTooltip));
            base.OnPropertyChanged(nameof(SonarStatusLabel));
        }
    }

    [JsonIgnore]
    public IBrush SonarStatusBrush => _sonarStatus switch
    {
        SonarConnectionStatus.Connected => Brushes.LimeGreen,
        SonarConnectionStatus.Disconnected => Brushes.OrangeRed,
        _ => Brushes.Gray,
    };

    [JsonIgnore]
    public string SonarStatusTooltip => _sonarStatus switch
    {
        SonarConnectionStatus.Connected => "Sonar connected — last switch succeeded",
        SonarConnectionStatus.Disconnected => "Sonar not reachable — is SteelSeries GG running?",
        _ => "Waiting for the first profile switch",
    };

    [JsonIgnore]
    public string SonarStatusLabel => _sonarStatus switch
    {
        SonarConnectionStatus.Connected => "Sonar connected",
        SonarConnectionStatus.Disconnected => "Not reachable",
        _ => "Waiting...",
    };

    // EQ bar heights — driven by AudioMeterService peak, ephemeral, never persisted.
    [JsonIgnore] public double EqBar1 => Math.Max(4.0, _audioPeak * 28.0 * 0.60);
    [JsonIgnore] public double EqBar2 => Math.Max(4.0, _audioPeak * 28.0 * 0.85);
    [JsonIgnore] public double EqBar3 => Math.Max(4.0, _audioPeak * 28.0 * 1.00);
    [JsonIgnore] public double EqBar4 => Math.Max(4.0, _audioPeak * 28.0 * 0.90);
    [JsonIgnore] public double EqBar5 => Math.Max(4.0, _audioPeak * 28.0 * 0.72);
    [JsonIgnore] public double EqBar6 => Math.Max(4.0, _audioPeak * 28.0 * 0.48);

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
        // Demo state is already fully formed (ActiveProfile, status, expanded card). Don't read
        // the real Sonar DB — that would overwrite it and leak the user's real selected config.
        if (homeViewModel.IsDemo) return homeViewModel;
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

    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        // ActiveProfile is ephemeral ([JsonIgnore]) and changes on every switch — notify but don't persist.
        if (propertyName is nameof(FilteredProfiles)
                         or nameof(SortModeLabel)
                         or nameof(SortModeTooltip)
                         or nameof(ActiveProfile)
                         or nameof(SonarStatusLabel)
                         or nameof(EqBar1) or nameof(EqBar2) or nameof(EqBar3)
                         or nameof(EqBar4) or nameof(EqBar5) or nameof(EqBar6))
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
