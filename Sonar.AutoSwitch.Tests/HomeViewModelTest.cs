using System.Collections.Generic;
using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch.Tests;

public class HomeViewModelTest
{
    [Fact]
    public void CycleSort_cycles_through_four_modes_without_returning_to_unsorted()
    {
        var home = new HomeViewModel();
        Assert.Equal("⇅", home.SortModeLabel);   // initial unsorted
        home.CycleSort(); Assert.Equal("↑",   home.SortModeLabel);
        home.CycleSort(); Assert.Equal("↓",   home.SortModeLabel);
        home.CycleSort(); Assert.Equal("⏰↓", home.SortModeLabel);
        home.CycleSort(); Assert.Equal("⏰↑", home.SortModeLabel);
        home.CycleSort(); Assert.Equal("↑",   home.SortModeLabel); // back to A→Z, not ⇅
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
