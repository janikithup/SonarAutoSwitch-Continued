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
