using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Sonar.AutoSwitch.Services;

public class StateManager
{
    private readonly string _appDataPath;
    private readonly Dictionary<Type, DelayedDeduplicateAction> _saveActions = new();
    private readonly Dictionary<Type, object?> _states = new();
    private readonly HashSet<Type> _readOnly = new();
    // Guards _states/_readOnly. GetOrLoadState ran lock-free, so a concurrent load of the same
    // type (UI startup vs. the audio Timer thread spinning up AutoSwitchService) produced two
    // instances and could corrupt the dictionary. Monitor is reentrant, so same-thread nesting is fine.
    private readonly object _lock = new();

    private StateManager()
    {
        _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Sonar.AutoSwitch");
    }

    public static StateManager Instance { get; } = new();

    // Pre-populate the cache with an in-memory instance that must never be persisted.
    // Used by --demo so the screenshot run never reads or writes the user's real state.
    public void SeedReadOnly<T>(T state)
    {
        lock (_lock)
        {
            _states[typeof(T)] = state;
            _readOnly.Add(typeof(T));
        }
    }

    public void SaveState<T>()
    {
        if (_readOnly.Contains(typeof(T))) return;
        if (GetState<T>() is not { } state) return;
        var path = GetJsonPath<T>();
        if (!_saveActions.TryGetValue(typeof(T), out var action))
            _saveActions[typeof(T)] = action = new DelayedDeduplicateAction();
        action.QueueAction(async () =>
        {
#pragma warning disable IL2026
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(state));
#pragma warning restore IL2026
        });
    }

    public void SaveStateNow<T>()
    {
        if (_readOnly.Contains(typeof(T))) return;
        if (GetState<T>() is not { } state) return;
#pragma warning disable IL2026
        File.WriteAllText(GetJsonPath<T>(), JsonSerializer.Serialize(state));
#pragma warning restore IL2026
    }

    public T GetOrLoadState<T>() where T : new()
    {
        lock (_lock)
        {
            if (GetState<T>() is { } existingState)
                return existingState;

            string jsonPath = Path.Combine(_appDataPath, typeof(T).Name + ".json");
#pragma warning disable IL2026
            // Deserialize runs arbitrary property setters while _lock is held. Those setters MUST NOT
            // touch a static AutoSwitchService member (e.g. AutoSwitchService.Log): doing so triggers
            // that singleton's type-init, which calls back into GetOrLoadState on another thread →
            // _lock vs. type-init-lock deadlock. Keep the deserialize path free of AutoSwitchService.
            T? loadState = !File.Exists(jsonPath) ? new T() : JsonSerializer.Deserialize<T>(File.ReadAllText(jsonPath));
#pragma warning restore IL2026
            T state = loadState ?? new T();
            _states[typeof(T)] = state;
            return state;
        }
    }

    public bool CheckStateExists<T>()
    {
        return File.Exists(Path.Combine(_appDataPath, typeof(T).Name + ".json"));
    }

    private string GetJsonPath<T>()
    {
        Directory.CreateDirectory(_appDataPath);
        return Path.Combine(_appDataPath, typeof(T).Name + ".json");
    }

    private T? GetState<T>()
    {
        lock (_lock)
        {
            if (_states.TryGetValue(typeof(T), out object? existing) && existing is T existingState)
                return existingState;
            return default;
        }
    }
}
