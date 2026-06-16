using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Sonar.AutoSwitch.Services;

namespace Sonar.AutoSwitch.ViewModels;

public class AutoSwitchProfileViewModel : ViewModelBase
{
    private string _exeName = "MyGame";
    private string _title = "";
    private SonarGamingConfiguration _sonarGamingConfiguration = new(null, "Unset");
    private string _sonarMatchHint = "";
    private bool _isExpanded;
    private bool _isConfirmingDelete;
    private bool _isAdvancedExpanded;

    // Loaded once; SQLite hit is too slow to repeat on every ExeName keystroke.
    private static readonly Lazy<IReadOnlyList<SonarGamingConfiguration>> _configCache = new(() =>
    {
        try { return SteelSeriesSonarService.Instance.AvailableGamingConfigurations.ToList(); }
        catch { return []; }
    });

    public string Title
    {
        get => _title;
        set
        {
            value ??= "";
            if (value == _title) return;
            _title = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
            // Auto-open the advanced section when a title is loaded or set for the first time.
            if (!string.IsNullOrEmpty(_title) && !_isAdvancedExpanded)
            {
                _isAdvancedExpanded = true;
                OnPropertyChanged(nameof(IsAdvancedExpanded));
            }
            TryAutoMatchSonarConfig();
        }
    }

    public string ExeName
    {
        get => _exeName;
        set
        {
            value ??= "";
            if (value == _exeName) return;
            _exeName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(ExeRunningHint));
            OnPropertyChanged(nameof(HasExeRunningHint));
            OnPropertyChanged(nameof(IsIncomplete));
            TryAutoMatchSonarConfig();
        }
    }

    public SonarGamingConfiguration SonarGamingConfiguration
    {
        get => _sonarGamingConfiguration;
        set
        {
            if (value is null || Equals(value, _sonarGamingConfiguration)) return;
            _sonarGamingConfiguration = value;
            SonarMatchHint = "";
            OnPropertyChanged(nameof(SonarGamingConfiguration));
            OnPropertyChanged(nameof(IsIncomplete));
        }
    }

    public DateTime? CreatedAt { get; set; }

    [JsonIgnore] public bool HasCreatedAt => CreatedAt.HasValue;

    [JsonIgnore]
    public string CreatedAtLabel => CreatedAt?.ToLocalTime().ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? "";

    [JsonIgnore]
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (value == _isExpanded) return;
            _isExpanded = value;
            OnPropertyChanged();
            if (!value) IsConfirmingDelete = false;
        }
    }

    [JsonIgnore]
    public bool IsConfirmingDelete
    {
        get => _isConfirmingDelete;
        set
        {
            if (value == _isConfirmingDelete) return;
            _isConfirmingDelete = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public bool IsAdvancedExpanded
    {
        get => _isAdvancedExpanded;
        set
        {
            if (value == _isAdvancedExpanded) return;
            _isAdvancedExpanded = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public Action? OnDeleteConfirmed { get; set; }

    [JsonIgnore]
    public RecentWindowInfo? SelectedRecentWindow
    {
        get => null;
        set
        {
            if (value is null) return;
            ExeName = value.ExeName;  // ExeName setter calls TryAutoMatchSonarConfig
            OnPropertyChanged(nameof(SelectedRecentWindow));
        }
    }

    [JsonIgnore]
    public string ExeRunningHint =>
        HomeViewModel.ProcessNames.Contains(_exeName, StringComparer.OrdinalIgnoreCase)
            ? "Running ✓" : "";

    [JsonIgnore] public bool HasExeRunningHint => ExeRunningHint.Length > 0;

    [JsonIgnore]
    public string SonarMatchHint
    {
        get => _sonarMatchHint;
        private set
        {
            if (value == _sonarMatchHint) return;
            _sonarMatchHint = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSonarMatchHint));
        }
    }

    [JsonIgnore] public bool HasSonarMatchHint => _sonarMatchHint.Length > 0;

    [JsonIgnore]
    public bool IsIncomplete => _exeName == "MyGame" || _sonarGamingConfiguration.Id is null;

    public void StartDelete()
    {
        // Profiles that were never configured can be removed without confirmation.
        if (_exeName == "MyGame" && _sonarGamingConfiguration.Id is null)
            ConfirmDelete();
        else
            IsConfirmingDelete = true;
    }

    public void CancelDelete() => IsConfirmingDelete = false;
    public void ConfirmDelete() => OnDeleteConfirmed?.Invoke();

    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? ExeName : Title;

    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName is nameof(IsExpanded) or nameof(IsConfirmingDelete) or nameof(SelectedRecentWindow)
                         or nameof(ExeRunningHint) or nameof(HasExeRunningHint)
                         or nameof(SonarMatchHint) or nameof(HasSonarMatchHint)
                         or nameof(IsAdvancedExpanded) or nameof(IsIncomplete)) return;
        StateManager.Instance.SaveState<HomeViewModel>();
    }

    public override string ToString() =>
        $"Title: {Title}, ExeName: {ExeName}, SonarGamingConfiguration: {SonarGamingConfiguration}, DisplayName: {DisplayName}";

    private void TryAutoMatchSonarConfig()
    {
        // Prefer exe name; fall back to window title if exe is blank.
        var term = !string.IsNullOrEmpty(_exeName) ? _exeName : _title;
        var match = FindBestMatch(term, _configCache.Value);
        if (match is null) return;
        SonarGamingConfiguration = match;   // setter no-ops if same config
        SonarMatchHint = $"Auto-matched: {match.Name}";
    }

    // Exposed internal for unit testing without a real Sonar service.
    internal static SonarGamingConfiguration? FindBestMatch(
        string exeName, IEnumerable<SonarGamingConfiguration> configs)
    {
        var norm = Normalize(exeName);
        if (norm.Length < 3) return null;
        return configs.FirstOrDefault(c =>
        {
            var cn = Normalize(c.Name);
            return cn.Contains(norm) || norm.Contains(cn);
        });
    }

    private static string Normalize(string s) =>
        Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9]", "");
}
