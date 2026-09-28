using UnityEngine;
using UnityEngine.SceneManagement;


public class MainMenuController : MonoBehaviour
{
    [Header("Scene Management")]
    [Tooltip("The name of the scene to load when the player clicks Start")]
    public string gameSceneName = "MainScene";


    public void StartGame()
    {
        Debug.Log($"Starting Game! Loading scene: {gameSceneName}");
        SceneManager.LoadScene(gameSceneName);
    }


    public void OpenSettings()
    {
        Debug.Log("Opening Settings... (Placeholder)");
        // TODO: Enable Settings UI Panel / Canvas
    }


    public void OpenCredits()
    {
        Debug.Log("Opening Credits... (Placeholder)");
        // TODO: Enable Credits UI Panel / Canvas
    }


    public void QuitGame()
    {
        Debug.Log("Quitting Application...");
        
        // This will quit the built executable
        Application.Quit();

#if UNITY_EDITOR
     
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
