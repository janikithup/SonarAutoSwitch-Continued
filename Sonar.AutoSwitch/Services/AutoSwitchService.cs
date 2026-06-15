using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch.Services;

public class AutoSwitchService
{
    private readonly HomeViewModel _homeViewModel;
    private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
    private SonarGamingConfiguration _selectedGamingConfiguration;
    private CancellationTokenSource _cancellationTokenSource;

    public AutoSwitchService()
    {
        WindowEventManager.Instance.ForegroundWindowChanged += InstanceOnForegroundWindowChanged;
        _homeViewModel = StateManager.Instance.GetOrLoadState<HomeViewModel>()!;
    }

    public static AutoSwitchService Instance { get; } = new();

    public void ToggleEnabled(bool enable)
    {
        if (enable)
            WindowEventManager.Instance.SubscribeToWindowEvents();
        else
            WindowEventManager.Instance.UnsubscribeToWindowsEvents();
    }

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Sonar.AutoSwitch", "debug.log");

    private static void Log(string message)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}\n"); } catch { }
    }

    private async void InstanceOnForegroundWindowChanged(object? sender, WindowInfo e)
    {
        string? windowExeName = e.ExeName;
        Log($"ForegroundChanged: exe={windowExeName} title={e.Title}");

        if (string.Equals(windowExeName, "explorer", StringComparison.OrdinalIgnoreCase))
            return;

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        await _semaphoreSlim.WaitAsync();
        try
        {
            IEnumerable<AutoSwitchProfileViewModel> autoSwitchProfileViewModels = _homeViewModel.AutoSwitchProfiles;
            if (StateManager.Instance.GetOrLoadState<SettingsViewModel>().UseGithubConfigs)
            {
                autoSwitchProfileViewModels =
                    autoSwitchProfileViewModels.Concat(AutoSwitchProfilesDatabase.Instance.GithubProfiles);
            }

            AutoSwitchProfileViewModel? autoSwitchProfileViewModel =
                autoSwitchProfileViewModels.FirstOrDefault(p =>
                    (string.IsNullOrEmpty(p.ExeName) ||
                     string.Equals(p.ExeName, windowExeName, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrEmpty(p.Title) || e.Title.Contains(p.Title, StringComparison.OrdinalIgnoreCase)));
            SonarGamingConfiguration? sonarGamingConfiguration = autoSwitchProfileViewModel?.SonarGamingConfiguration;
            sonarGamingConfiguration ??= _homeViewModel.DefaultSonarGamingConfiguration;
            Log($"Matched profile: {autoSwitchProfileViewModel?.Title ?? "(none→default)"} → config={sonarGamingConfiguration?.Name} id={sonarGamingConfiguration?.Id}");

            if (string.IsNullOrEmpty(sonarGamingConfiguration.Id) ||
                _selectedGamingConfiguration == sonarGamingConfiguration)
            {
                Log($"Early return: id empty={string.IsNullOrEmpty(sonarGamingConfiguration.Id)} sameConfig={_selectedGamingConfiguration == sonarGamingConfiguration}");
                return;
            }

            try
            {
                string selectedGamingConfigurationId =
                    SteelSeriesSonarService.Instance.GetSelectedGamingConfiguration();
                Log($"Current Sonar config: {selectedGamingConfigurationId}");
                _selectedGamingConfiguration = sonarGamingConfiguration;
                if (sonarGamingConfiguration.Id == selectedGamingConfigurationId)
                {
                    Log("Already on correct config, skipping");
                    return;
                }
                Log($"Switching to {sonarGamingConfiguration.Name}...");
                await SteelSeriesSonarService.Instance.ChangeSelectedGamingConfiguration(sonarGamingConfiguration,
                    _cancellationTokenSource.Token);
                Log("Switch complete");
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.GetType().Name}: {ex.Message}");
            }
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }
}