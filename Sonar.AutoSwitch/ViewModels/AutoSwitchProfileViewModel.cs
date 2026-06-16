using System;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Sonar.AutoSwitch.Services;

namespace Sonar.AutoSwitch.ViewModels;

public class AutoSwitchProfileViewModel : ViewModelBase
{
    private string _exeName = "MyGame";
    private string _title = "";
    private SonarGamingConfiguration _sonarGamingConfiguration = new(null, "Unset");
    private bool _isExpanded;
    private bool _isConfirmingDelete;

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
        }
    }

    public SonarGamingConfiguration SonarGamingConfiguration
    {
        get => _sonarGamingConfiguration;
        set
        {
            if (Equals(value, _sonarGamingConfiguration)) return;
            _sonarGamingConfiguration = value;
            OnPropertyChanged(nameof(SonarGamingConfiguration));
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
    public Action? OnDeleteConfirmed { get; set; }

    public void StartDelete() => IsConfirmingDelete = true;
    public void CancelDelete() => IsConfirmingDelete = false;
    public void ConfirmDelete() => OnDeleteConfirmed?.Invoke();

    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? ExeName : Title;

    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName is nameof(IsExpanded) or nameof(IsConfirmingDelete)) return;
        StateManager.Instance.SaveState<HomeViewModel>();
    }

    public override string ToString() =>
        $"Title: {Title}, ExeName: {ExeName}, SonarGamingConfiguration: {SonarGamingConfiguration}, DisplayName: {DisplayName}";
}
