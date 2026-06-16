# Backlog

## Bugs

**B1 — Startup registration overwrites on every launch**
`StartupService.RegisterInStartup(true)` runs on every app start if StartAtStartup=true.
Any launch from a non-installed path (FlaUI test, dev build) silently replaces the registry entry.
Fix: only register when the user explicitly toggles the setting in Settings, not on every launch.

**B2 — FlaUI test pollutes real app state**
The test launches the published dev exe, which shares `%LOCALAPPDATA%\Sonar.AutoSwitch\` with the
installed app. Running tests re-registers the Windows startup entry to the dev path.
Fix: pass a `--state-dir <temp>` argument to the test exe so state is isolated.

**B3 — Port scan runs on every switch regardless of cache (suspected 8–14s delay)**
`SteelSeriesSonarService.ChangeSelectedGamingConfiguration` calls `NetworkHelper.GetPortById`
unconditionally before checking `_lastWorkingPort`. The cache port is prepended to the scan
results as a fast-path, but the full TCP scan still runs every time. Fix: try `_lastWorkingPort`
first; only run `GetPortById` if it fails. Use `debug.log` "PortScan:" entries to measure actual
delay before changing anything.

**B4 — `--show` flag unnecessarily saves SettingsViewModel**
The `--show` branch in App.axaml.cs calls `SaveState<SettingsViewModel>()` even when not firstLoad.
Minor, but it re-saves state during FlaUI test runs. Fix: gate SaveState on firstLoad only.

## UX

**U1 — No tray tooltip or status indicator**
User cannot tell if the app is running or how many profiles are active without opening the window.
Fix: set tray icon tooltip to "Sonar AutoSwitch — X profiles active" (or "disabled").

**U2 — Settings page doesn't show registered startup path**
If the startup entry points to the wrong exe, the user has no way to know.
Fix: show the registered path in Settings next to the "Start with Windows" toggle.

**U3 — Close-to-tray vs exit is implicit**
No setting or label explains that closing the window hides to tray. Users expect close = exit.
Fix: add "Close to tray" toggle in Settings (default on); if off, closing exits the process.

**U4 — No first-run tray notification**
On first launch the window appears, but subsequent launches are silent. Users may not know the
app is still running.
Fix: show a tray balloon tip on first silent-tray launch: "Sonar AutoSwitch is running in the
background."

## Accessibility

**A1 — Expander headers named 'Avalonia.Controls.TextBlock'**
Screen readers and automation can't identify profiles by name.
Fix: add `AutomationProperties.Name="{Binding DisplayName}"` on the Expander.

**A2 — Add and Delete buttons have no accessible name**
Both show the SymbolIcon type name to automation/screen readers.
Fix: add `AutomationProperties.Name="Add profile"` / `"Delete profile"`.

**A3 — Exe name and Window title edit fields have no accessible name**
Fix: add `AutomationProperties.Name="Game executable name"` etc.

## Test infrastructure

**T1 — FlaUI screenshots miss popup/flyout states**
`Capture.Element(_window)` only captures the main window. Dropdowns and popups render in
separate top-level windows and are invisible to element capture.
Fix: use `Capture.Screen()` for any state that involves a popup.

**T2 — Screenshots capture whatever scroll position the app is in**
Test doesn't scroll to top before screenshotting, so content may be partially off-screen.
Fix: scroll to top (or to a known position) before each state screenshot.

**T3 — 2-second startup sleep is fragile**
App startup time varies. On a slow machine the window may not be ready.
Fix: replace `Thread.Sleep(2000)` with polling on window visibility.

**T4 — Delete button finder was wrong (fixed), but locators are generally fragile**
Avalonia exposes almost no AutomationIds. Locators rely on control type + name heuristics
that break if the XAML changes.
Fix: add explicit `AutomationProperties.AutomationId` to key interactive elements (also fixes A1–A3).

**T5 — No assertion that screenshots contain expected content**
Tests pass even if the screenshot is a blank window. Screenshots are only useful if I review
them manually — there's no automated check.
Fix: add pixel-level or region checks (e.g. assert a named region is non-empty / changed).
