using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class GameCompleteState : RoundBaseState
{
    [TextArea] public string completionMessage = "You Win!\nPress Enter to play again.";

    private string previousText;
    private Color previousColor;
    private bool previousActiveState;
    private bool hasTextReference;

    public override void EnterState(RoundStateManager manager)
    {
        manager.UpdateInstructionText("Game complete!");

        if (manager.gameOverText == null)
        {
            Debug.LogWarning("Game complete text cannot be displayed because gameOverText is not assigned");
            return;
        }

        previousText = manager.gameOverText.text;
        previousColor = manager.gameOverText.color;
        previousActiveState = manager.gameOverText.gameObject.activeSelf;
        hasTextReference = true;

        Color visibleColor = previousColor;
        visibleColor.a = Color.white.a;

        manager.gameOverText.text = completionMessage;
        manager.gameOverText.color = visibleColor;
        manager.gameOverText.gameObject.SetActive(true);
        
        manager.MoveCamera(CameraLocation.BallThrowingCameraLocation);
    }

    public override void UpdateState(RoundStateManager manager)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }

    public override void ExitState(RoundStateManager manager)
    {
        if (!hasTextReference || manager.gameOverText == null)
        {
            return;
        }

        manager.gameOverText.text = previousText;
        manager.gameOverText.color = previousColor;
        manager.gameOverText.gameObject.SetActive(previousActiveState);
        hasTextReference = false;
    }
}
