using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Avalonia.Threading;

namespace Sonar.AutoSwitch.Services;

// Non-positional record. [JsonConstructor] is explicit so System.Text.Json constructor
// discovery is unambiguous under PublishSingleFile and future STJ versions.
public record RecentWindowInfo
{
    [JsonConstructor]
    public RecentWindowInfo(string ExeName, string Title)
    {
        this.ExeName = ExeName;
        this.Title = Title;
    }

    public string ExeName { get; init; }
    public string Title { get; init; }

    [JsonIgnore]
    public string Display => string.IsNullOrEmpty(Title) ? ExeName : $"{ExeName} — {Title}";
}

public class RecentWindowsState
{
    public List<RecentWindowInfo> Windows { get; set; } = new();
}

public static class RecentWindowsService
{
    private const int MaxEntries = 20;

    // _lock guards all mutations to RecentWindowsState.Windows and _collection init.
    // WINEVENT_OUTOFCONTEXT callbacks arrive on a thread-pool thread; List<T> is not
    // thread-safe, so every read-modify-write path must hold this lock.
    private static readonly object _lock = new();
    private static ObservableCollection<RecentWindowInfo>? _collection;

    /// <summary>
    /// Observable list bound to the picker ComboBox via {x:Static}.
    /// CollectionChanged is always posted to the UI thread by AddWindow.
    /// Tests read from StateManager.GetOrLoadState&lt;RecentWindowsState&gt;() directly.
    /// </summary>
    public static ObservableCollection<RecentWindowInfo> RecentWindows
    {
        get
        {
            lock (_lock)
            {
                return _collection ??= BuildCollectionLocked();
            }
        }
    }

    private static ObservableCollection<RecentWindowInfo> BuildCollectionLocked()
    {
        var state = StateManager.Instance.GetOrLoadState<RecentWindowsState>();
        ClampState(state);
        return new ObservableCollection<RecentWindowInfo>(state.Windows);
    }

    internal static bool IsSystemExePath(string? exePath)
    {
        if (exePath == null) return false;
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return exePath.StartsWith(winDir, StringComparison.OrdinalIgnoreCase);
    }

    public static void AddWindow(string? exeName, string title, string? exePath = null)
    {
        if (string.IsNullOrEmpty(exeName))
            return;
        if (IsSystemExePath(exePath))
            return;

        title ??= "";

        var entry = new RecentWindowInfo(exeName, title);
        List<RecentWindowInfo> snapshot;

        lock (_lock)
        {
            var state = StateManager.Instance.GetOrLoadState<RecentWindowsState>();

            // Dedup by ExeName only (case-insensitive): one slot per game exe.
            // Title is replaced so the picker label reflects the most recent window title.
            state.Windows.RemoveAll(w =>
                string.Equals(w.ExeName, exeName, StringComparison.OrdinalIgnoreCase));

            state.Windows.Insert(0, entry);
            ClampState(state);
            snapshot = new List<RecentWindowInfo>(state.Windows);

            _collection ??= new ObservableCollection<RecentWindowInfo>();
        }

        // Post collection sync to UI thread so CollectionChanged fires on the correct thread
        // for bound ItemsControls. In tests the dispatcher has no pump; the Post is enqueued
        // but never executed — tests verify list state via StateManager directly.
        var col = _collection!;
        void ApplySnapshot()
        {
            col.Clear();
            foreach (var item in snapshot)
                col.Add(item);
        }

        if (Dispatcher.UIThread.CheckAccess())
            ApplySnapshot();
        else
            Dispatcher.UIThread.Post(ApplySnapshot, DispatcherPriority.Background);

        StateManager.Instance.SaveState<RecentWindowsState>();
    }

    private static void ClampState(RecentWindowsState state)
    {
        if (state.Windows.Count > MaxEntries)
            state.Windows.RemoveRange(MaxEntries, state.Windows.Count - MaxEntries);
    }

    /// <summary>Test use only. Resets the static ObservableCollection so the next
    /// RecentWindows access rebuilds from the current StateManager cache.</summary>
    internal static void ResetCollectionForTest()
    {
        lock (_lock)
        {
            _collection = null;
        }
    }
}
