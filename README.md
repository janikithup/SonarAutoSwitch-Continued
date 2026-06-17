# Sonar.AutoSwitch, Continued

A continuation of [adirh3/Sonar.AutoSwitch](https://github.com/adirh3/Sonar.AutoSwitch). Automatically switches your SteelSeries Sonar gaming audio profile when a game comes into focus.

<p align="center">
  <img src="screenshot.png" width="340" alt="screenshot" />
</p>

## Features

- Switches Sonar EQ profiles automatically when you alt-tab between games
- Set a **Default config** for everything outside of gaming (music, desktop, etc.)
- Match by **game executable**, **window title**, or both — with AND/OR logic
- Active profile shown in the header and window title
- Community profiles from the upstream repo, auto-matched to your local Sonar configs
- Sonar connection status dot — green when connected, grey when idle, red when unreachable

## Download

Grab `Sonar.AutoSwitch.exe` from the [latest release](https://github.com/janikithup/SonarAutoSwitch-Continued/releases/latest). Self-contained — no .NET install needed.

## How to use

1. Run the exe. It sits in the system tray; left-click the icon to open the window.
2. Set a **Default config** — the Sonar preset that applies when no game profile matches.
3. Add a profile per game with the **+** button. For each profile:
   - Pick the game from **Fill from recent app** (populated as you alt-tab), browse to the `.exe`, or type the process name without `.exe` (autocompletes from running processes).
   - The Sonar config auto-matches by name. Override it from the dropdown if needed.
   - For games where the exe can't be read (e.g. Valorant), open **Advanced** and match on window title instead.
4. Switch to a game — Sonar switches with it.

## Build

Requires the .NET 8 SDK.

```powershell
cd Sonar.AutoSwitch
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
