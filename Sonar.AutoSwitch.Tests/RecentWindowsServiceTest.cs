using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Sonar.AutoSwitch.Services;

namespace Sonar.AutoSwitch.Tests;

[Collection("StateManagerSerialTests")]
public class RecentWindowsServiceTest
{
    private static string StatePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sonar.AutoSwitch", "RecentWindowsState.json");

    private static void ClearCache()
    {
        var field = typeof(StateManager).GetField("_states",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (Dictionary<Type, object?>)field.GetValue(StateManager.Instance)!;
        dict.Remove(typeof(RecentWindowsState));
    }

    private static void ResetState()
    {
        if (File.Exists(StatePath)) File.Delete(StatePath);
        ClearCache();
        RecentWindowsService.ResetCollectionForTest();
        StateManager.Instance.GetOrLoadState<RecentWindowsState>().Windows.Clear();
    }

    private static void Cleanup()
    {
        ClearCache();
        RecentWindowsService.ResetCollectionForTest();
        if (File.Exists(StatePath)) File.Delete(StatePath);
    }

    private static List<RecentWindowInfo> Windows =>
        StateManager.Instance.GetOrLoadState<RecentWindowsState>().Windows;

    [Fact]
    public void AddWindow_ignores_null_or_empty_exeName()
    {
        ResetState();
        try
        {
            RecentWindowsService.AddWindow(null, "Some Title");
            RecentWindowsService.AddWindow("", "Some Title");
            Assert.Empty(Windows);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public void AddWindow_inserts_new_entry_with_correct_fields()
    {
        ResetState();
        try
        {
            RecentWindowsService.AddWindow("notepad", "Untitled - Notepad");
            Assert.Single(Windows);
            Assert.Equal("notepad", Windows[0].ExeName);
            Assert.Equal("Untitled - Notepad", Windows[0].Title);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public void AddWindow_deduplicates_by_exe_case_insensitive_and_refreshes_title()
    {
        ResetState();
        try
        {
            RecentWindowsService.AddWindow("Notepad", "First Title");
            RecentWindowsService.AddWindow("NOTEPAD", "Second Title");
            Assert.Single(Windows);
            Assert.Equal("NOTEPAD", Windows[0].ExeName);
            Assert.Equal("Second Title", Windows[0].Title);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public void AddWindow_places_newest_entry_first()
    {
        ResetState();
        try
        {
            RecentWindowsService.AddWindow("appA", "Title A");
            RecentWindowsService.AddWindow("appB", "Title B");
            Assert.Equal("appB", Windows[0].ExeName);
            Assert.Equal("appA", Windows[1].ExeName);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public void AddWindow_caps_list_at_20_dropping_oldest_entry()
    {
        ResetState();
        try
        {
            for (int i = 1; i <= 21; i++)
                RecentWindowsService.AddWindow($"game{i:D2}", $"Game {i}");
            Assert.Equal(20, Windows.Count);
            Assert.Equal("game21", Windows[0].ExeName);
            Assert.DoesNotContain(Windows, w => w.ExeName == "game01");
        }
        finally { Cleanup(); }
    }

    [Fact]
    public void Windows_persist_across_cache_clear()
    {
        ResetState();
        try
        {
            RecentWindowsService.AddWindow("game1", "Title 1");
            StateManager.Instance.SaveStateNow<RecentWindowsState>();
            ClearCache();
            RecentWindowsService.ResetCollectionForTest();
            var reloaded = StateManager.Instance.GetOrLoadState<RecentWindowsState>().Windows;
            Assert.Single(reloaded);
            Assert.Equal("game1", reloaded[0].ExeName);
            Assert.Equal("Title 1", reloaded[0].Title);
        }
        finally { Cleanup(); }
    }

    // ── system-window filter ──────────────────────────────────────────────────

    [Fact]
    public void IsSystemExePath_returns_true_for_windows_binaries()
    {
        var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        Assert.True(RecentWindowsService.IsSystemExePath(Path.Combine(win, "System32", "Taskmgr.exe")));
        Assert.True(RecentWindowsService.IsSystemExePath(Path.Combine(win, "ImmersiveControlPanel", "SystemSettings.exe")));
        Assert.True(RecentWindowsService.IsSystemExePath(Path.Combine(win, "explorer.exe")));
        Assert.True(RecentWindowsService.IsSystemExePath(
            Path.Combine(win, "SystemApps", "Microsoft.Windows.Search_cw5n1h2txyewy", "SearchHost.exe")));
    }

    [Fact]
    public void IsSystemExePath_returns_false_for_games_launchers_and_null()
    {
        Assert.False(RecentWindowsService.IsSystemExePath(null));
        Assert.False(RecentWindowsService.IsSystemExePath(@"C:\Program Files\Steam\steam.exe"));
        Assert.False(RecentWindowsService.IsSystemExePath(@"D:\Games\MyGame\game.exe"));
        // Game Pass titles live here — must NOT be filtered
        Assert.False(RecentWindowsService.IsSystemExePath(@"C:\Program Files\WindowsApps\SomeGame\game.exe"));
    }

    [Fact]
    public void AddWindow_ignores_system_exe_path()
    {
        ResetState();
        try
        {
            var taskmgr = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "Taskmgr.exe");
            RecentWindowsService.AddWindow("Taskmgr", "Task Manager", taskmgr);
            Assert.Empty(Windows);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public void AddWindow_includes_game_with_non_system_path()
    {
        ResetState();
        try
        {
            RecentWindowsService.AddWindow("game", "My Game", @"C:\Games\MyGame\game.exe");
            Assert.Single(Windows);
            Assert.Equal("game", Windows[0].ExeName);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public void AddWindow_null_exePath_is_not_filtered()
    {
        ResetState();
        try
        {
            // No exePath provided (e.g. path lookup failed) — still recorded
            RecentWindowsService.AddWindow("notepad", "Untitled", null);
            Assert.Single(Windows);
        }
        finally { Cleanup(); }
    }
}

[CollectionDefinition("StateManagerSerialTests", DisableParallelization = true)]
public class StateManagerSerialCollection { }
