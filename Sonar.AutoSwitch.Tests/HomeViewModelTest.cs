using System.Collections.Generic;
using Avalonia.Media;
using Sonar.AutoSwitch.Services;
using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch.Tests;

public class HomeViewModelTest
{
    [Fact]
    public void SonarStatus_defaults_to_idle_grey()
    {
        var home = new HomeViewModel();
        Assert.Equal(SonarConnectionStatus.Idle, home.SonarStatus);
        Assert.Equal(Brushes.Gray, home.SonarStatusBrush);
    }

    [Fact]
    public void SonarStatus_connected_is_green_with_a_reassuring_tooltip()
    {
        var home = new HomeViewModel { SonarStatus = SonarConnectionStatus.Connected };
        Assert.Equal(Brushes.LimeGreen, home.SonarStatusBrush);
        Assert.Contains("connected", home.SonarStatusTooltip, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SonarStatus_disconnected_is_red_and_names_the_likely_cause()
    {
        var home = new HomeViewModel { SonarStatus = SonarConnectionStatus.Disconnected };
        Assert.Equal(Brushes.OrangeRed, home.SonarStatusBrush);
        Assert.Contains("Sonar", home.SonarStatusTooltip);
    }

    [Fact]
    public void SonarStatus_change_raises_brush_and_tooltip_notifications()
    {
        var home = new HomeViewModel();
        var changed = new List<string?>();
        home.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        home.SonarStatus = SonarConnectionStatus.Connected;
        Assert.Contains(nameof(HomeViewModel.SonarStatusBrush), changed);
        Assert.Contains(nameof(HomeViewModel.SonarStatusTooltip), changed);
    }

    [Fact]
    public void AddAutoSwitchProfile_stamps_CreatedAt()
    {
        var home = new HomeViewModel();
        home.AutoSwitchProfiles.Clear();
        var before = DateTime.UtcNow;
        home.AddAutoSwitchProfile();
        Assert.NotNull(home.AutoSwitchProfiles[0].CreatedAt);
        Assert.True(home.AutoSwitchProfiles[0].CreatedAt >= before);
    }

    // T8 — search filtering behavior

    [Fact]
    public void SearchText_filters_profiles_by_ExeName()
    {
        var home = new HomeViewModel();
        home.AutoSwitchProfiles.Clear();
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "Cyberpunk2077" });
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "Overwatch" });

        home.SearchText = "cyber";

        var results = home.FilteredProfiles.ToList();
        Assert.Single(results);
        Assert.Equal("Cyberpunk2077", results[0].ExeName);
    }

    [Fact]
    public void SearchText_filters_profiles_by_Title()
    {
        var home = new HomeViewModel();
        home.AutoSwitchProfiles.Clear();
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "game1", Title = "Halo Infinite" });
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "game2", Title = "Elden Ring" });

        home.SearchText = "halo";

        var results = home.FilteredProfiles.ToList();
        Assert.Single(results);
        Assert.Equal("Halo Infinite", results[0].Title);
    }

    [Fact]
    public void SearchText_filter_is_case_insensitive()
    {
        var home = new HomeViewModel();
        home.AutoSwitchProfiles.Clear();
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "Cyberpunk2077" });
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "Overwatch" }); // non-matching sibling

        home.SearchText = "CYBER";
        var upper = home.FilteredProfiles.ToList();
        Assert.Single(upper);
        Assert.Equal("Cyberpunk2077", upper[0].ExeName);

        home.SearchText = "cyber"; // lowercase against mixed-case name
        var lower = home.FilteredProfiles.ToList();
        Assert.Single(lower);
        Assert.Equal("Cyberpunk2077", lower[0].ExeName);
    }

    [Fact]
    public void SearchText_returns_multiple_matches()
    {
        var home = new HomeViewModel();
        home.AutoSwitchProfiles.Clear();
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "Cyberpunk2077" });
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "CyberHunter" });
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "Overwatch" });

        home.SearchText = "cyber";

        Assert.Equal(2, home.FilteredProfiles.Count());
    }

    [Fact]
    public void SearchText_matches_across_both_fields_in_same_result()
    {
        var home = new HomeViewModel();
        home.AutoSwitchProfiles.Clear();
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "game_a",   Title = "Halo Infinite" });
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "halo_game", Title = "" });
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "Overwatch", Title = "" });

        home.SearchText = "halo";

        Assert.Equal(2, home.FilteredProfiles.Count());
    }

    [Fact]
    public void Clearing_SearchText_restores_all_profiles()
    {
        var home = new HomeViewModel();
        home.AutoSwitchProfiles.Clear();
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "Cyberpunk2077" });
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "Overwatch" });

        home.SearchText = "cyber";
        Assert.Single(home.FilteredProfiles.ToList());

        home.SearchText = "";
        Assert.Equal(2, home.FilteredProfiles.Count());
    }

    [Fact]
    public void Whitespace_SearchText_restores_all_profiles()
    {
        var home = new HomeViewModel();
        home.AutoSwitchProfiles.Clear();
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "Cyberpunk2077" });
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "Overwatch" });

        home.SearchText = "   ";

        Assert.Equal(2, home.FilteredProfiles.Count());
    }

    [Fact]
    public void SearchText_change_raises_SearchText_and_FilteredProfiles_notifications()
    {
        var home = new HomeViewModel();
        var changed = new List<string?>();
        home.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        home.SearchText = "anything";

        Assert.Contains(nameof(HomeViewModel.SearchText), changed);
        Assert.Contains(nameof(HomeViewModel.FilteredProfiles), changed);
    }

    [Fact]
    public void SearchText_no_match_returns_empty()
    {
        var home = new HomeViewModel();
        home.AutoSwitchProfiles.Clear();
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "Overwatch" });

        home.SearchText = "xyzzy_no_match";

        Assert.Empty(home.FilteredProfiles);
    }

    // B9 regression: FilteredProfiles must not NRE when ExeName or Title is null.
    [Fact]
    public void FilteredProfiles_does_not_throw_when_profile_has_null_ExeName_or_Title()
    {
        var home = new HomeViewModel();
        home.AutoSwitchProfiles.Clear();
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = null!, Title = null! });
        home.AutoSwitchProfiles.Add(new AutoSwitchProfileViewModel { ExeName = "game", Title = null! });

        home.SearchText = "game";

        // FilteredProfiles should not throw and should return only profiles matching "game"
        var results = home.FilteredProfiles;
        Assert.NotEmpty(results);
    }
}
