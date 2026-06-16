using System.Text.Json.Serialization;
using Sonar.AutoSwitch.Services;
using Sonar.AutoSwitch.Services.Win32;

namespace Sonar.AutoSwitch.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private bool _enabled = true;
    private bool _startAtStartup = true;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (value == _enabled) return;
            _enabled = value;
            AutoSwitchService.Instance.ToggleEnabled(_enabled);
            OnPropertyChanged();
        }
    }

    public bool StartAtStartup
    {
        get => _startAtStartup;
        set
        {
            if (value == _startAtStartup) return;
            _startAtStartup = value;
            StartupService.RegisterInStartup(_startAtStartup);
            OnPropertyChanged();
        }
    }

    private bool _useGithubConfigs = true;

    public bool UseGithubConfigs
    {
        get => _useGithubConfigs;
        set
        {
            if (value == _useGithubConfigs) return;
            _useGithubConfigs = value;
            OnPropertyChanged();
        }
    }

    // ponytail: auto-property so JSON sets it without triggering side-effects via [JsonConstructor] path.
    public bool HasShownTrayNotification { get; set; }

    [JsonIgnore]
    public string StartupDescription
    {
        get
        {
            var path = StartupService.GetRegisteredPath();
            return path is null ? "Not registered with Windows startup" : $"Registered: {path}";
        }
    }

    private bool _closeToTray = true;

    // ponytail: JsonConstructor sets backing fields directly; RegisterInStartup/ToggleEnabled never fire on load.
    [JsonConstructor]
    private SettingsViewModel(bool enabled = true, bool startAtStartup = true,
                              bool useGithubConfigs = true, bool closeToTray = true)
    {
        _enabled = enabled;
        _startAtStartup = startAtStartup;
        _useGithubConfigs = useGithubConfigs;
        _closeToTray = closeToTray;
    }

    public SettingsViewModel() { }

    public bool CloseToTray
    {
        get => _closeToTray;
        set
        {
            if (value == _closeToTray) return;
            _closeToTray = value;
            OnPropertyChanged();
        }
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        StateManager.Instance.SaveStateNow<SettingsViewModel>();
    }
}