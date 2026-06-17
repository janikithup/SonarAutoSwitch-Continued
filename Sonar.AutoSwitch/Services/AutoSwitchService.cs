using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Sonar.AutoSwitch.Services.Win32;
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
        Win32WindowEventManager.Instance.ForegroundWindowChanged += InstanceOnForegroundWindowChanged;
        _homeViewModel = StateManager.Instance.GetOrLoadState<HomeViewModel>()!;
    }

    public static AutoSwitchService Instance { get; } = new();

    public void ToggleEnabled(bool enable)
    {
        if (enable)
        {
            Win32WindowEventManager.Instance.SubscribeToWindowEvents();
            Win32WindowEventManager.Instance.FireCurrentForeground();
        }
        else
        {
            Win32WindowEventManager.Instance.UnsubscribeToWindowsEvents();
            _selectedGamingConfiguration = default!;
            _homeViewModel.SonarStatus = SonarConnectionStatus.Idle;
        }
    }

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Sonar.AutoSwitch", "debug.log");

    internal static void Log(string message)
    {
        try
        {
            if (new FileInfo(LogPath).Length > 1_000_000)
            {
                File.Copy(LogPath, LogPath + ".bak", overwrite: true);
                File.WriteAllText(LogPath, "");
            }
            // ponytail: no framework, just a size cap; add proper rotation if log analysis becomes a need
            File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}\n");
        }
        catch { }
    }

    // The status a completed switch should set, or null when it was canceled (superseded by a
    // newer switch) and the dot should be left alone. A canceled switch is not a Sonar failure.
    internal static SonarConnectionStatus? StatusForSwitch(bool switched, bool canceled) =>
        canceled ? null
        : switched ? SonarConnectionStatus.Connected
        : SonarConnectionStatus.Disconnected;

    public static bool ProfileMatches(AutoSwitchProfileViewModel p, string? exeName, string title)
    {
        if (string.IsNullOrEmpty(p.ExeName) && string.IsNullOrEmpty(p.Title)) return false;
        bool exeOk = string.IsNullOrEmpty(p.ExeName) || string.Equals(p.ExeName, exeName, StringComparison.OrdinalIgnoreCase);
        bool titleOk = string.IsNullOrEmpty(p.Title) || title.Contains(p.Title, StringComparison.OrdinalIgnoreCase);
        if (!p.TitleMatchOr) return exeOk && titleOk;
        // OR: each non-empty field is an independent condition; any hit suffices.
        bool exeHit = !string.IsNullOrEmpty(p.ExeName) && string.Equals(p.ExeName, exeName, StringComparison.OrdinalIgnoreCase);
        bool titleHit = !string.IsNullOrEmpty(p.Title) && title.Contains(p.Title, StringComparison.OrdinalIgnoreCase);
        bool anyFilled = !string.IsNullOrEmpty(p.ExeName) || !string.IsNullOrEmpty(p.Title);
        return !anyFilled || exeHit || titleHit;
    }

    private async void InstanceOnForegroundWindowChanged(object? sender, WindowInfo e)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            string? windowExeName = e.ExeName;
            Log($"ForegroundChanged: exe={windowExeName} title={e.Title}");

            if (string.Equals(windowExeName, "explorer", StringComparison.OrdinalIgnoreCase))
                return;

            RecentWindowsService.AddWindow(windowExeName, e.Title, e.ExePath);

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;   // this switch's token, even if a newer one replaces it
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
                    autoSwitchProfileViewModels.FirstOrDefault(p => AutoSwitchService.ProfileMatches(p, windowExeName, e.Title));
                SonarGamingConfiguration? sonarGamingConfiguration = autoSwitchProfileViewModel?.SonarGamingConfiguration;
                sonarGamingConfiguration ??= _homeViewModel.DefaultSonarGamingConfiguration;
                Log($"Matched: {autoSwitchProfileViewModel?.Title ?? "(none→default)"} → {sonarGamingConfiguration?.Name} [{sw.ElapsedMilliseconds}ms]");

                _homeViewModel.ActiveProfile = sonarGamingConfiguration;

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
                    Log($"CurrentConfig: {selectedGamingConfigurationId} [{sw.ElapsedMilliseconds}ms]");
                    _selectedGamingConfiguration = sonarGamingConfiguration;
                    if (sonarGamingConfiguration.Id == selectedGamingConfigurationId)
                    {
                        // DB was readable and we're already on the right config — Sonar is up.
                        _homeViewModel.SonarStatus = SonarConnectionStatus.Connected;
                        Log("Already correct, skipping");
                        return;
                    }
                    Log($"Switching to {sonarGamingConfiguration.Name}... [{sw.ElapsedMilliseconds}ms]");
                    bool switched = await SteelSeriesSonarService.Instance.ChangeSelectedGamingConfiguration(
                        sonarGamingConfiguration, token);
                    // A superseded (canceled) switch isn't a failure — leave the dot for the newer switch.
                    if (StatusForSwitch(switched, token.IsCancellationRequested) is { } status)
                        _homeViewModel.SonarStatus = status;
                    Log($"Switch complete [{sw.ElapsedMilliseconds}ms]");
                }
                catch (Exception ex)
                {
                    // A canceled (superseded) operation is not a real failure; only real errors mean Sonar is down.
                    if (ex is not OperationCanceledException)
                        _homeViewModel.SonarStatus = SonarConnectionStatus.Disconnected;
                    Log($"ERROR: {ex.GetType().Name}: {ex.Message}");
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }
        catch (Exception ex)
        {
            Log($"UNHANDLED in ForegroundWindowChanged: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
