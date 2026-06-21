using Sonar.AutoSwitch.Services;
using Sonar.AutoSwitch.ViewModels;

namespace Sonar.AutoSwitch.Tests;

public class AutoSwitchServiceKeepWhileRunningTest
{
    private static AutoSwitchProfileViewModel Profile(bool keep, string exe = "SoTGame") =>
        new() { KeepWhileRunning = keep, ExeName = exe };

    [Fact]
    public void Keeps_when_flag_set_and_exe_running()
        => Assert.True(AutoSwitchService.ShouldKeepLocked(Profile(keep: true), _ => true));

    [Fact]
    public void Does_not_keep_when_exe_not_running()
        => Assert.False(AutoSwitchService.ShouldKeepLocked(Profile(keep: true), _ => false));

    [Fact]
    public void Does_not_keep_when_flag_off()
        => Assert.False(AutoSwitchService.ShouldKeepLocked(Profile(keep: false), _ => true));

    [Fact]
    public void Does_not_keep_when_locked_is_null()
        => Assert.False(AutoSwitchService.ShouldKeepLocked(null, _ => true));

    [Fact]
    public void Does_not_keep_when_ExeName_empty()
        => Assert.False(AutoSwitchService.ShouldKeepLocked(Profile(keep: true, exe: ""), _ => true));
}
