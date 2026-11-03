using System.Collections.Generic;
using UnityEngine;

public static class GameplayPause
{
    private static readonly Dictionary<int, RoundStateManager> managers = new Dictionary<int, RoundStateManager>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => managers.Clear();

    internal static void Register(RoundStateManager manager) => managers[manager.gameObject.scene.handle] = manager;

    internal static void Unregister(RoundStateManager manager)
    {
        int scene = manager.gameObject.scene.handle;
        if (managers.TryGetValue(scene, out var registered) && ReferenceEquals(registered, manager))
            managers.Remove(scene);
    }

    private static RoundStateManager FindManager(Component source)
    {
        if (source == null) return null;
        managers.TryGetValue(source.gameObject.scene.handle, out var manager);
        return manager;
    }

    public static bool IsPaused(Component source)
    {
        var manager = FindManager(source);
        return manager != null && manager.IsGameplayPaused;
    }

    public static bool IsInputBlocked(Component source)
    {
        var manager = FindManager(source);
        return manager != null && manager.IsGameplayInputBlocked;
    }
}
