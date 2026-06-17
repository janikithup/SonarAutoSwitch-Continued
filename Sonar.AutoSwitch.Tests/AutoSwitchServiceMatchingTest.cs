using Sonar.AutoSwitch.Services;
using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch.Tests;

public class AutoSwitchServiceMatchingTest
{
    // Helper: create a profile with the given ExeName and Title (empty string = wildcard).
    private static AutoSwitchProfileViewModel Profile(string exeName, string title, bool orMode = false) =>
        new AutoSwitchProfileViewModel { ExeName = exeName, Title = title, TitleMatchOr = orMode };

    // 1. Empty ExeName matches any exe (as long as Title is set; both-empty is unconfigured)
    [Fact]
    public void EmptyExeName_matches_any_exe()
    {
        var p = Profile("", "Some Window");
        Assert.True(AutoSwitchService.ProfileMatches(p, "SomeGame", "Some Window Title"));
        Assert.True(AutoSwitchService.ProfileMatches(p, "OtherGame", "Some Window Title"));
    }

    // 2. ExeName matches case-insensitively
    [Fact]
    public void ExeName_matches_case_insensitively()
    {
        var p = Profile("mygame", "");
        Assert.True(AutoSwitchService.ProfileMatches(p, "MYGAME", "any title"));
        Assert.True(AutoSwitchService.ProfileMatches(p, "MyGame", "any title"));
        Assert.True(AutoSwitchService.ProfileMatches(p, "mygame", "any title"));
    }

    // 3. ExeName mismatch returns false
    [Fact]
    public void ExeName_mismatch_returns_false()
    {
        var p = Profile("MyGame", "");
        Assert.False(AutoSwitchService.ProfileMatches(p, "OtherGame", "any title"));
    }

    // 4. Empty Title matches any title (as long as ExeName is set; both-empty is unconfigured)
    [Fact]
    public void EmptyTitle_matches_any_title()
    {
        var p = Profile("SomeGame", "");
        Assert.True(AutoSwitchService.ProfileMatches(p, "SomeGame", "Totally Different Window"));
        Assert.True(AutoSwitchService.ProfileMatches(p, "somegame", ""));
    }

    // 5. Title matches case-insensitively (Contains)
    [Fact]
    public void Title_matches_case_insensitively_contains()
    {
        var p = Profile("", "crimson");
        Assert.True(AutoSwitchService.ProfileMatches(p, "SomeGame", "Crimson Desert - Main Menu"));
        Assert.True(AutoSwitchService.ProfileMatches(p, "SomeGame", "CRIMSON DESERT"));
        Assert.True(AutoSwitchService.ProfileMatches(p, "SomeGame", "play crimson now"));
    }

    // 6. Title mismatch returns false
    [Fact]
    public void Title_mismatch_returns_false()
    {
        var p = Profile("", "Warcraft");
        Assert.False(AutoSwitchService.ProfileMatches(p, "SomeGame", "Starcraft II"));
    }

    // 7. Both ExeName and Title must match (AND logic)
    [Fact]
    public void Both_ExeName_and_Title_must_match()
    {
        var p = Profile("MyGame", "Lobby");

        // Both match
        Assert.True(AutoSwitchService.ProfileMatches(p, "mygame", "Game Lobby Screen"));

        // ExeName matches but Title does not
        Assert.False(AutoSwitchService.ProfileMatches(p, "mygame", "Main Menu"));

        // Title matches but ExeName does not
        Assert.False(AutoSwitchService.ProfileMatches(p, "OtherGame", "Game Lobby Screen"));

        // Neither matches
        Assert.False(AutoSwitchService.ProfileMatches(p, "OtherGame", "Main Menu"));
    }

    // 8. Profile with both fields empty never matches (unconfigured — would be a wildcard otherwise)
    [Fact]
    public void Both_fields_empty_never_matches()
    {
        var p = Profile("", "");
        Assert.False(AutoSwitchService.ProfileMatches(p, null, "any title"));
        Assert.False(AutoSwitchService.ProfileMatches(p, "AnyGame", "Any Title"));
    }

    // 9. Profile with non-empty ExeName does NOT match null exeName
    [Fact]
    public void NonEmpty_ExeName_does_not_match_null_exeName()
    {
        var p = Profile("MyGame", "");
        Assert.False(AutoSwitchService.ProfileMatches(p, null, "any title"));
    }

    // OR mode: exe matches, title does not → still matches
    [Fact]
    public void OR_mode_exe_match_is_sufficient()
    {
        var p = Profile("MyGame", "Lobby", orMode: true);
        Assert.True(AutoSwitchService.ProfileMatches(p, "mygame", "Main Menu"));
    }

    // OR mode: title matches, exe does not → still matches
    [Fact]
    public void OR_mode_title_match_is_sufficient()
    {
        var p = Profile("MyGame", "Lobby", orMode: true);
        Assert.True(AutoSwitchService.ProfileMatches(p, "OtherGame", "Game Lobby Screen"));
    }

    // OR mode: neither matches → no match
    [Fact]
    public void OR_mode_neither_match_returns_false()
    {
        var p = Profile("MyGame", "Lobby", orMode: true);
        Assert.False(AutoSwitchService.ProfileMatches(p, "OtherGame", "Main Menu"));
    }

    // OR mode: both fields empty → still no match (unconfigured profile guard fires before OR logic)
    [Fact]
    public void OR_mode_both_empty_never_matches()
    {
        var p = Profile("", "", orMode: true);
        Assert.False(AutoSwitchService.ProfileMatches(p, "AnyGame", "Any Title"));
    }
}
