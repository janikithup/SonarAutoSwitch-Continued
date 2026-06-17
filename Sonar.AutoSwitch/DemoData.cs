using System;
using System.Collections.ObjectModel;
using Sonar.AutoSwitch.Services;
using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch;

// Placeholder state for the --demo launch used to generate the README screenshot.
// Never persisted (seeded read-only) and never reads the user's real Sonar/profile data.
internal static class DemoData
{
    // The Sonar EQ presets the demo pretends the user has. Game-named entries let the exe
    // auto-match fire so the expanded card shows the real "Auto-matched: …" hint; the generic
    // ones (Competitive/Cinematic) stand in as ordinary presets. App wires these into
    // SteelSeriesSonarService.ConfigQuery in --demo so matching and the dropdowns use them.
    public static readonly SonarGamingConfiguration[] Configs =
    {
        new("demo-cyberpunk",  "Cyberpunk 2077"),
        new("demo-cs2",        "Counter-Strike 2"),
        new("demo-elden",      "Elden Ring"),
        new("demo-bg3",        "Baldur's Gate 3"),
        new("demo-competitive","Competitive"),
        new("demo-cinematic",  "Cinematic"),
    };

    public static HomeViewModel HomeViewModel()
    {
        // (pretty name, exe, expanded?) — each exe normalizes to a Configs name, so setting
        // ExeName triggers the auto-match that fills the Sonar config and the hint.
        var demos = new[]
        {
            ("Cyberpunk 2077",   "Cyberpunk2077",   true),
            ("Counter-Strike 2", "Counter-Strike2", false),
            ("Elden Ring",       "EldenRing",       false),
            ("Baldur's Gate 3",  "BaldursGate3",    false),
        };

        var profiles = new ObservableCollection<AutoSwitchProfileViewModel>();
        var baseDate = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < demos.Length; i++)
        {
            var (name, exe, expanded) = demos[i];
            profiles.Add(new AutoSwitchProfileViewModel
            {
                Title = name,
                ExeName = exe,              // setter auto-matches against Configs -> fills config + hint
                CreatedAt = baseDate.AddDays(i),
                IsExpanded = expanded,
                IsAdvancedExpanded = false, // Title setter auto-opens it; keep the card clean
            });
        }

        return new HomeViewModel
        {
            IsDemo = true,
            AutoSwitchProfiles = profiles,
            DefaultSonarGamingConfiguration = Configs[4], // "Competitive"
            ActiveProfile = Configs[0],                    // "Cyberpunk 2077"
            SonarStatus = SonarConnectionStatus.Connected, // green dot
        };
    }
}
