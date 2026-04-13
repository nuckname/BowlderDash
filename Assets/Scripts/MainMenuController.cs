using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles the main menu UI actions such as starting the game, quitting, and opening sub-menus.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Scene Management")]
    [Tooltip("The name of the scene to load when the player clicks Start. If you only have one scene, make sure to name your game scene properly.")]
    public string gameSceneName = "SampleScene";

    /// <summary>
    /// Hook this to your 'Start' Button's OnClick event.
    /// </summary>
    public void StartGame()
    {
        Debug.Log($"Starting Game! Loading scene: {gameSceneName}");
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>
    /// Hook this to your 'Settings' Button's OnClick event.
    /// </summary>
    public void OpenSettings()
    {
        Debug.Log("Opening Settings... (Placeholder)");
        // TODO: Enable Settings UI Panel / Canvas
    }

    /// <summary>
    /// Hook this to your 'Credits' Button's OnClick event.
    /// </summary>
    public void OpenCredits()
    {
        Debug.Log("Opening Credits... (Placeholder)");
        // TODO: Enable Credits UI Panel / Canvas
    }

    /// <summary>
    /// Hook this to your 'Quit' Button's OnClick event.
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("Quitting Application...");
        
        // This will quit the built executable
        Application.Quit();

#if UNITY_EDITOR
        // This stops the play mode if you are testing inside the Unity Editor
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
