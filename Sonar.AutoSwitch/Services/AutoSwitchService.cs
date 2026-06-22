using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Sonar.AutoSwitch.Services.Win32;
using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch.Services;

public class AutoSwitchService
{
    private readonly HomeViewModel _homeViewModel;
    private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
    private SonarGamingConfiguration _selectedGamingConfiguration;
    private CancellationTokenSource _cancellationTokenSource;
    private AutoSwitchProfileViewModel? _lockedProfile;
    private CancellationTokenSource? _keepCheckCts;

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
            _lockedProfile = null;
            _keepCheckCts?.Cancel();
            _keepCheckCts?.Dispose();
            _keepCheckCts = null;
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

    // Exposed internal for unit testing — pass a fake isRunning to avoid real Process calls in tests.
    internal static bool ShouldKeepLocked(AutoSwitchProfileViewModel? locked, bool keepWhileRunning, Func<string, bool>? isRunning = null)
    {
        if (!keepWhileRunning || locked is null || !locked.IsEnabled || string.IsNullOrEmpty(locked.ExeName)) return false;
        isRunning ??= name =>
        {
            var procs = Process.GetProcessesByName(name);
            foreach (var p in procs) p.Dispose();
            return procs.Length > 0;
        };
        return isRunning(locked.ExeName);
    }

    public static bool ProfileMatches(AutoSwitchProfileViewModel p, string? exeName, string title)
    {
        if (!p.IsEnabled) return false;
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

            // Explorer (desktop, taskbar, shell) is never a game but can be the foreground window
            // at startup. Do NOT early-return here — let the proactive scan run so a game that was
            // already running when the app started gets detected. Guard only against adding explorer
            // to recent windows and against reverting to default (handled after the scan below).
            bool isExplorer = string.Equals(windowExeName, "explorer", StringComparison.OrdinalIgnoreCase);
            if (!isExplorer)
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

                bool keepWhileRunning = StateManager.Instance.GetOrLoadState<SettingsViewModel>().KeepWhileRunning;
                bool isSelfWindow = string.Equals(windowExeName, "Sonar.AutoSwitch", StringComparison.OrdinalIgnoreCase);
                if (autoSwitchProfileViewModel is not null)
                {
                    _lockedProfile = autoSwitchProfileViewModel;
                    // Game is in foreground — no need to poll for its exit
                    _keepCheckCts?.Cancel();
                    _keepCheckCts?.Dispose();
                    _keepCheckCts = null;
                }
                else if (ShouldKeepLocked(_lockedProfile, keepWhileRunning))
                {
                    Log($"KeepWhileRunning: {_lockedProfile!.ExeName} still running, holding {_lockedProfile.SonarGamingConfiguration.Name}");
                    autoSwitchProfileViewModel = _lockedProfile;
                    // Profile held but game is not in foreground — recheck in 2.5s so we revert
                    // promptly when the game exits without a subsequent window-focus change.
                    _keepCheckCts?.Cancel();
                    _keepCheckCts?.Dispose();
                    var cts = _keepCheckCts = new CancellationTokenSource();
                    _ = Task.Delay(2500, cts.Token).ContinueWith(
                        _ => Dispatcher.UIThread.Post(Win32WindowEventManager.Instance.FireCurrentForeground),
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnRanToCompletion,
                        TaskScheduler.Default);
                }
                else if (keepWhileRunning || isSelfWindow)
                {
                    // Proactive: _lockedProfile was never set (e.g. app started with game already running,
                    // or profile was just re-enabled). Scan all profiles for any whose exe is currently running.
                    // When our own management UI is in the foreground, always run this scan — opening the
                    // tray window or toggling IsEnabled should never discard an active game profile.
                    var found = autoSwitchProfileViewModels.FirstOrDefault(p => ShouldKeepLocked(p, true));
                    if (found != null)
                    {
                        _lockedProfile = found;
                        autoSwitchProfileViewModel = found;
                        Log($"ProactiveKeep: {found.ExeName} is running, activating {found.SonarGamingConfiguration.Name}");
                        _keepCheckCts?.Cancel();
                        _keepCheckCts?.Dispose();
                        var cts = _keepCheckCts = new CancellationTokenSource();
                        _ = Task.Delay(2500, cts.Token).ContinueWith(
                            _ => Dispatcher.UIThread.Post(Win32WindowEventManager.Instance.FireCurrentForeground),
                            CancellationToken.None,
                            TaskContinuationOptions.OnlyOnRanToCompletion,
                            TaskScheduler.Default);
                    }
                }

                SonarGamingConfiguration? sonarGamingConfiguration = autoSwitchProfileViewModel?.SonarGamingConfiguration;
                // Explorer came to foreground and proactive scan found nothing running — don't revert
                // to default just because the user clicked the desktop or taskbar.
                if (sonarGamingConfiguration == null && isExplorer) return;
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
                    if (sonarGamingConfiguration.Id == selectedGamingConfigurationId)
                    {
                        // DB was readable and we're already on the right config — Sonar is up.
                        _selectedGamingConfiguration = sonarGamingConfiguration;
                        _homeViewModel.SonarStatus = SonarConnectionStatus.Connected;
                        Log("Already correct, skipping");
                        return;
                    }
                    Log($"Switching to {sonarGamingConfiguration.Name}... [{sw.ElapsedMilliseconds}ms]");
                    bool switched = await SteelSeriesSonarService.Instance.ChangeSelectedGamingConfiguration(
                        sonarGamingConfiguration, token);
                    // Only update the cache on a confirmed switch — a cancelled switch leaves the field
                    // stale otherwise, causing the next event for the same config to early-return.
                    if (!token.IsCancellationRequested && switched)
                        _selectedGamingConfiguration = sonarGamingConfiguration;
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
