using UnityEngine;
using UnityEngine.SceneManagement;

internal static class SceneRunReload
{
    internal const string MainScenePath = "Assets/Scenes/MainScene.unity";

    internal readonly struct Startup
    {
        internal readonly int token;
        internal readonly bool startNewRun;
        internal Startup(int token, bool startNewRun) { this.token = token; this.startNewRun = startNewRun; }
    }

    private static int nextToken, token, sourceHandle, destinationHandle;
    private static bool active, claimed, startNewRun, ownsPause;
    private static float timeScaleBeforeReload;
    private static bool listenerPauseBeforeReload;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Finish();
        nextToken = 0;
    }

    internal static bool IsBlockingScene(Scene scene) => active && scene.path == MainScenePath;

    internal static bool TryStage(Scene source, bool autoStart, out int requestToken)
    {
        requestToken = 0;
        if (active || !source.IsValid() || !source.isLoaded || source.path != MainScenePath) return false;
        unchecked { nextToken++; }
        if (nextToken == 0) nextToken++;
        requestToken = token = nextToken;
        sourceHandle = source.handle;
        destinationHandle = -1;
        active = true;
        claimed = ownsPause = false;
        startNewRun = autoStart;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.quitting += Finish;
        return true;
    }

    internal static void TakePauseOwnership(int requestToken, float previousTimeScale, bool previousListenerPause)
    {
        if (!active || token != requestToken || ownsPause) return;
        timeScaleBeforeReload = previousTimeScale;
        listenerPauseBeforeReload = previousListenerPause;
        ownsPause = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!active) return;
        if (mode != LoadSceneMode.Single || scene.path != MainScenePath || scene.handle == sourceHandle)
        {
            Finish();
            return;
        }
        destinationHandle = scene.handle;
        int managers = 0;
        foreach (var manager in Object.FindObjectsByType<RoundStateManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (manager.gameObject.scene == scene && manager.isActiveAndEnabled) managers++;
        if (managers == 1) return;
        Debug.LogError("Reloaded MainScene needs exactly one active RoundStateManager to complete startup");
        Finish();
    }

    internal static bool TryConsume(Scene scene, out Startup startup)
    {
        startup = default;
        if (!active || claimed || scene.path != MainScenePath || scene.handle != destinationHandle) return false;
        claimed = true;
        startup = new Startup(token, startNewRun);
        return true;
    }

    internal static bool IsCurrent(Startup startup, Scene scene) => active && claimed && startup.token == token &&
        scene.handle == destinationHandle && scene.path == MainScenePath;

    internal static void Complete(Startup startup)
    {
        if (active && claimed && startup.token == token) Finish();
    }

    internal static void Cancel(int requestToken)
    {
        if (active && token == requestToken) Finish();
    }

    internal static void CancelForDestination(Scene scene)
    {
        if (active && scene.handle == destinationHandle) Finish();
    }

    private static void Finish()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Application.quitting -= Finish;
        active = claimed = false;
        sourceHandle = destinationHandle = -1;
        token = 0;
        startNewRun = false;
        if (ownsPause)
        {
            if (Time.timeScale == 0f) Time.timeScale = timeScaleBeforeReload;
            if (AudioListener.pause) AudioListener.pause = listenerPauseBeforeReload;
        }
        ownsPause = false;
    }
}
