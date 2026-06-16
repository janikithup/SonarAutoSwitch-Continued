using System;
using System.IO;
using System.Text;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

namespace Sonar.AutoSwitch.Tests;

// Launches the real published app with --show, drives key UI states, asserts accessibility.
// Run with: dotnet test --filter "Category=UI"
[Trait("Category", "UI")]
public class UIExplorationTest : IDisposable
{
    private const string ExePath = @"G:\Claude\SonarAutoSwitch\Sonar.AutoSwitch\bin\Release\net8.0\win-x64\publish\Sonar.AutoSwitch.exe";
    private const string OutDir = @"C:\Temp\flaui-shots";

    private readonly Application _app;
    private readonly UIA3Automation _automation;
    private readonly ITestOutputHelper _out;
    private readonly Window _window;

    public UIExplorationTest(ITestOutputHelper output)
    {
        _out = output;
        Directory.CreateDirectory(OutDir);
        _automation = new UIA3Automation();
        _app = Application.Launch(ExePath, "--show");
        _window = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(15));
        WaitUntilReady(_window);
    }

    // Regression test for A1: Expander headers were named 'Avalonia.Controls.TextBlock'
    // instead of the profile's DisplayName, making profiles invisible to accessibility tools.
    // Fix: AutomationProperties.Name="{Binding DisplayName}" on Expander in Home.axaml.
    [Fact]
    public void Expander_name_matches_profile_display_name()
    {
        var expandedGroup = FindExpandedGroup();
        Assert.NotNull(expandedGroup);

        var name = Safe(() => expandedGroup!.Name);
        Assert.NotEmpty(name);
        Assert.NotEqual("Avalonia.Controls.TextBlock", name);

        _out.WriteLine($"Expanded profile name: '{name}' — PASS");
    }

    // Regression test for A2: Delete button was named 'FluentAvalonia.UI.Controls.SymbolIcon'.
    // Fix: AutomationProperties.Name="Delete profile" on the button in Home.axaml.
    [Fact]
    public void Delete_button_has_accessible_name()
    {
        var deleteBtn = FindDeleteButton();
        Assert.NotNull(deleteBtn);

        var name = Safe(() => deleteBtn!.Name);
        Assert.Equal("Delete profile", name);

        _out.WriteLine($"Delete button name: '{name}' — PASS");
    }

    // Regression test for A3: Window title TextBox was unnamed. AutoCompleteBox (exe name)
    // doesn't propagate AutomationProperties.Name to its inner UIA node — verify by AutoId instead.
    [Fact]
    public void Edit_fields_are_findable()
    {
        var titleEdit = _window.FindFirstDescendant(cf => cf.ByName("Window title"));
        Assert.NotNull(titleEdit);

        var exeEdit = _window.FindFirstDescendant(cf => cf.ByAutomationId("PART_TextBox"));
        Assert.NotNull(exeEdit);

        _out.WriteLine("Title field (by name) and exe field (by AutoId) found — PASS");
    }

    [Fact]
    public void Screenshot_key_states()
    {
        Assert.NotNull(_window);

        // 1. Initial state
        ShotWindow("1-initial");

        // 2. Open nav pane
        var hamburger = _window.FindFirstDescendant(cf => cf.ByAutomationId("TogglePaneButton"))?.AsButton();
        hamburger?.Click();
        Thread.Sleep(300);
        ShotWindow("2-pane-open");
        hamburger?.Click();
        Thread.Sleep(300);

        // 3. Click delete to show confirmation
        var deleteBtn = FindDeleteButton();
        deleteBtn?.Click();
        Thread.Sleep(300);
        ShotWindow("4-delete-confirmation");

        var cancelBtn = _window.FindFirstDescendant(cf =>
            cf.ByName("Cancel").And(cf.ByControlType(ControlType.Button)))?.AsButton();
        cancelBtn?.Click();
        Thread.Sleep(200);

        // 4. Type in exe field — use Capture.Screen() because dropdown is a popup window
        var exeEdit = _window.FindFirstDescendant(cf => cf.ByName("Game executable name"))
                      ?.FindFirstDescendant(cf => cf.ByAutomationId("PART_TextBox"))
                      ?.AsTextBox()
                   ?? _window.FindFirstDescendant(cf => cf.ByAutomationId("PART_TextBox"))?.AsTextBox();

        if (exeEdit != null)
        {
            exeEdit.Click();
            Thread.Sleep(200);
            exeEdit.Enter("explorer");
            Thread.Sleep(700);
            ShotScreen("5-autocomplete-dropdown"); // screen capture includes popup
            exeEdit.Enter("");
        }

        _out.WriteLine($"Screenshots saved to {OutDir}");
    }

    [Fact]
    public void Dump_automation_tree()
    {
        Assert.NotNull(_window);
        var sb = new StringBuilder();
        sb.AppendLine($"Window: Name='{Safe(() => _window.Name)}'");
        DumpChildren(_window, 1, sb);
        var result = sb.ToString();
        _out.WriteLine(result);
        File.WriteAllText(Path.Combine(OutDir, "tree.txt"), result);
    }

    // Avalonia reports UIA bounding rects in logical pixels; Capture.Element() captures
    // that logical-sized region at physical scale, clipping ~20% at 125% DPI.
    // Capture.Screen() is DPI-agnostic and always shows the full window.
    private void ShotWindow(string name) => ShotScreen(name);

    private void ShotScreen(string name)
    {
        var path = Path.Combine(OutDir, $"{name}.png");
        Capture.Screen().ToFile(path);
        _out.WriteLine($"Shot: {path}");
    }

    private AutomationElement? FindExpandedGroup()
    {
        var groups = _window.FindAllDescendants(cf => cf.ByControlType(ControlType.Group));
        foreach (var group in groups)
            if (group.FindAllChildren().Length >= 3)
                return group;
        return null;
    }

    private Button? FindDeleteButton()
    {
        var btn = _window.FindFirstDescendant(cf =>
            cf.ByName("Delete profile").And(cf.ByControlType(ControlType.Button)));
        return btn?.AsButton();
    }

    private static void WaitUntilReady(Window window, int timeoutMs = 8000)
    {
        var deadline = Environment.TickCount + timeoutMs;
        while (Environment.TickCount < deadline)
        {
            try
            {
                if (!window.IsOffscreen) return;
            }
            catch { /* element not ready yet */ }
            Thread.Sleep(100);
        }
    }

    private static string Safe(Func<string?> fn)
    {
        try { return fn() ?? ""; }
        catch { return "?"; }
    }

    private static void DumpChildren(AutomationElement el, int depth, StringBuilder sb)
    {
        if (depth > 10) return;
        foreach (var child in el.FindAllChildren())
        {
            sb.AppendLine($"{new string(' ', depth * 2)}[{Safe(() => child.ControlType.ToString())}] " +
                          $"Name='{Safe(() => child.Name)}' AutoId='{Safe(() => child.AutomationId)}'");
            DumpChildren(child, depth + 1, sb);
        }
    }

    public void Dispose() => _app?.Kill();
}
