using System;
using System.Collections.Generic;
using System.Globalization;
using Sonar.AutoSwitch.Services;
using Sonar.AutoSwitch.ViewModels;
using Xunit;

namespace Sonar.AutoSwitch.Tests;

public class AutoSwitchProfileViewModelTest
{
    [Fact]
    public void DisplayName_ReturnsExeName_WhenTitleIsEmpty()
    {
        var vm = new AutoSwitchProfileViewModel { ExeName = "MyGame", Title = "" };
        Assert.Equal("MyGame", vm.DisplayName);
    }

    [Fact]
    public void DisplayName_ReturnsTitle_WhenTitleIsSet()
    {
        var vm = new AutoSwitchProfileViewModel { ExeName = "MyGame", Title = "My Game Window" };
        Assert.Equal("My Game Window", vm.DisplayName);
    }

    [Fact]
    public void DisplayName_ReturnsExeName_WhenTitleIsWhitespace()
    {
        var vm = new AutoSwitchProfileViewModel { ExeName = "MyGame", Title = "   " };
        Assert.Equal("MyGame", vm.DisplayName);
    }

    [Fact]
    public void StartDelete_SetsIsConfirmingDelete_True()
    {
        var vm = new AutoSwitchProfileViewModel { ExeName = "Overwatch",
            SonarGamingConfiguration = new SonarGamingConfiguration("id-1", "Gaming") };
        vm.StartDelete();
        Assert.True(vm.IsConfirmingDelete);
    }

    [Fact]
    public void CancelDelete_SetsIsConfirmingDelete_False()
    {
        var vm = new AutoSwitchProfileViewModel();
        vm.StartDelete();
        vm.CancelDelete();
        Assert.False(vm.IsConfirmingDelete);
    }

    [Fact]
    public void ConfirmDelete_InvokesOnDeleteConfirmed()
    {
        var vm = new AutoSwitchProfileViewModel();
        bool invoked = false;
        vm.OnDeleteConfirmed = () => invoked = true;
        vm.ConfirmDelete();
        Assert.True(invoked);
    }

    [Fact]
    public void ConfirmDelete_DoesNotThrow_WhenOnDeleteConfirmedIsNull()
    {
        var vm = new AutoSwitchProfileViewModel();
        vm.OnDeleteConfirmed = null;
        var ex = Record.Exception(() => vm.ConfirmDelete());
        Assert.Null(ex);
    }

    [Fact]
    public void CollapsingExpander_ClearsIsConfirmingDelete()
    {
        var vm = new AutoSwitchProfileViewModel { ExeName = "Overwatch",
            SonarGamingConfiguration = new SonarGamingConfiguration("id-1", "Gaming") };
        vm.IsExpanded = true;
        vm.StartDelete();
        Assert.True(vm.IsConfirmingDelete);
        vm.IsExpanded = false;
        Assert.False(vm.IsConfirmingDelete);
    }

    [Fact]
    public void ExpandingExpander_DoesNotClearIsConfirmingDelete()
    {
        var vm = new AutoSwitchProfileViewModel { ExeName = "Overwatch",
            SonarGamingConfiguration = new SonarGamingConfiguration("id-1", "Gaming") };
        vm.IsExpanded = true;
        vm.StartDelete();
        Assert.True(vm.IsConfirmingDelete);
    }

    [Fact]
    public void CreatedAtLabel_ReturnsEmptyString_WhenCreatedAtIsNull()
    {
        var vm = new AutoSwitchProfileViewModel { CreatedAt = null };
        Assert.Equal("", vm.CreatedAtLabel);
    }

    [Fact]
    public void CreatedAtLabel_FormatsDate_InInvariantCulture()
    {
        // Use a UTC value so ToLocalTime() still lands on the same date in any timezone
        // by picking midnight UTC — the local time will be the same day or one day ahead.
        // Use a specific date and verify the format pattern instead of an exact string.
        var utcDate = new DateTime(2026, 6, 16, 12, 0, 0, DateTimeKind.Utc);
        var vm = new AutoSwitchProfileViewModel { CreatedAt = utcDate };
        var label = vm.CreatedAtLabel;
        // Must match "d MMM yyyy" in InvariantCulture — verify pattern, not exact value
        // (local time conversion may shift day by ±1 depending on machine timezone)
        Assert.Matches(@"^\d{1,2} [A-Z][a-z]{2} \d{4}$", label);
    }

    [Fact]
    public void CreatedAtLabel_FormatsDate_CorrectlyForKnownValue()
    {
        // Use local time directly to avoid timezone conversion ambiguity
        var localDate = new DateTime(2026, 6, 16, 12, 0, 0, DateTimeKind.Local);
        var vm = new AutoSwitchProfileViewModel { CreatedAt = localDate };
        var expected = localDate.ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        Assert.Equal(expected, vm.CreatedAtLabel);
    }

    [Fact]
    public void HasCreatedAt_IsFalse_WhenCreatedAtIsNull()
    {
        var vm = new AutoSwitchProfileViewModel { CreatedAt = null };
        Assert.False(vm.HasCreatedAt);
    }

    [Fact]
    public void HasCreatedAt_IsTrue_WhenCreatedAtIsSet()
    {
        var vm = new AutoSwitchProfileViewModel { CreatedAt = DateTime.UtcNow };
        Assert.True(vm.HasCreatedAt);
    }

    [Fact]
    public void SelectedRecentWindow_setter_fills_ExeName_only_leaving_Title_empty()
    {
        var vm = new AutoSwitchProfileViewModel();
        var recent = new RecentWindowInfo("crimson", "Crimson Desert");
        vm.SelectedRecentWindow = recent;
        Assert.Equal("crimson", vm.ExeName);
        Assert.Equal("", vm.Title);  // dynamic titles shouldn't auto-fill; user adds manually
    }

    [Fact]
    public void SelectedRecentWindow_getter_always_returns_null()
    {
        var vm = new AutoSwitchProfileViewModel();
        var recent = new RecentWindowInfo("crimson", "Crimson Desert");
        vm.SelectedRecentWindow = recent;
        Assert.Null(vm.SelectedRecentWindow);
    }

    // --- FindBestMatch (static, no Sonar service dependency) ---

    private static List<SonarGamingConfiguration> FakeConfigs() =>
    [
        new(null, "Unset"),
        new("1", "Crimson Desert"),
        new("2", "Counter-Strike 2"),
        new("3", "Firefox"),
    ];

    [Fact]
    public void FindBestMatch_returns_null_for_short_exe_name()
    {
        Assert.Null(AutoSwitchProfileViewModel.FindBestMatch("ab", FakeConfigs()));
    }

    [Fact]
    public void FindBestMatch_matches_exe_contained_in_config_name()
    {
        var result = AutoSwitchProfileViewModel.FindBestMatch("crimson", FakeConfigs());
        Assert.Equal("Crimson Desert", result?.Name);
    }

    [Fact]
    public void FindBestMatch_matches_config_name_contained_in_exe()
    {
        // exe "firefox123" contains normalized config name "firefox"
        var result = AutoSwitchProfileViewModel.FindBestMatch("firefox123", FakeConfigs());
        Assert.Equal("Firefox", result?.Name);
    }

    [Fact]
    public void FindBestMatch_is_case_insensitive()
    {
        var result = AutoSwitchProfileViewModel.FindBestMatch("CRIMSON", FakeConfigs());
        Assert.Equal("Crimson Desert", result?.Name);
    }

    [Fact]
    public void FindBestMatch_returns_null_when_no_match()
    {
        Assert.Null(AutoSwitchProfileViewModel.FindBestMatch("minecraft", FakeConfigs()));
    }

    [Fact]
    public void ExeRunningHint_returns_empty_for_unknown_exe()
    {
        var vm = new AutoSwitchProfileViewModel { ExeName = "xxxxnotaprocess_z9z9" };
        Assert.Equal("", vm.ExeRunningHint);
    }

    [Fact]
    public void IsAdvancedExpanded_auto_opens_when_title_is_set()
    {
        var vm = new AutoSwitchProfileViewModel();
        Assert.False(vm.IsAdvancedExpanded);
        vm.Title = "Valorant";
        Assert.True(vm.IsAdvancedExpanded);
    }

    // --- IsIncomplete / direct-delete ---

    [Fact]
    public void IsIncomplete_true_when_exeName_is_default()
    {
        var vm = new AutoSwitchProfileViewModel(); // ExeName = "", Title = "" (blank defaults)
        Assert.True(vm.IsIncomplete);
    }

    [Fact]
    public void IsIncomplete_true_when_sonarConfig_unset()
    {
        // Use a name that won't auto-match any real Sonar config so Id stays null.
        var vm = new AutoSwitchProfileViewModel { ExeName = "xxxx_no_match_9999" };
        Assert.True(vm.IsIncomplete);
    }

    [Fact]
    public void IsIncomplete_false_when_both_configured()
    {
        var vm = new AutoSwitchProfileViewModel
        {
            ExeName = "Overwatch",
            SonarGamingConfiguration = new SonarGamingConfiguration("id-1", "Gaming")
        };
        Assert.False(vm.IsIncomplete);
    }

    [Fact]
    public void StartDelete_skips_confirmation_when_profile_is_fully_default()
    {
        var vm = new AutoSwitchProfileViewModel(); // ExeName="", Title="", SonarConfig unset
        bool deleted = false;
        vm.OnDeleteConfirmed = () => deleted = true;
        vm.StartDelete();
        Assert.True(deleted);
        Assert.False(vm.IsConfirmingDelete);
    }

    [Fact]
    public void StartDelete_uses_confirmation_when_profile_has_data()
    {
        var vm = new AutoSwitchProfileViewModel { ExeName = "Overwatch" };
        vm.StartDelete();
        Assert.True(vm.IsConfirmingDelete);
    }
}
