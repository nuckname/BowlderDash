using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;

[System.Serializable]
public class GameOverState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        // Fade in Text
        manager.gameOverText.gameObject.SetActive(true);
        
        Color startColor = manager.gameOverText.color;
        startColor.a = 0f;
        manager.gameOverText.color = startColor;
        
        manager.StartCoroutine(FadeTextRoutine(manager));
        
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private IEnumerator FadeTextRoutine(RoundStateManager manager)
    {
        float fadeInDuration = 3.5f;
        float holdDuration = 2.5f;
        float fadeOutDuration = 2.0f;
        float elapsed = 0f;

        Color textColor = manager.gameOverText.color;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            textColor.a = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            manager.gameOverText.color = textColor;
            yield return null;
        }
        
        textColor.a = 1f;
        manager.gameOverText.color = textColor;

        yield return new WaitForSeconds(holdDuration);

        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            textColor.a = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            manager.gameOverText.color = textColor;
            yield return null;
        }
        
        textColor.a = 0f;
        manager.gameOverText.color = textColor;
        manager.gameOverText.gameObject.SetActive(false);
    }

    public override void UpdateState(RoundStateManager manager)
    {
        
    }

    public override void FixedUpdateState(RoundStateManager manager)
    {
        
    }

    public override void OnCollisionEnter(RoundStateManager manager, Collision collision)
    {
        
    }

    public override void OnCollisionExit(RoundStateManager manager, Collision collision)
    {
        
    }

    public override void OnTriggerEnter(RoundStateManager manager, Collider other)
    {
        
    }

    public override void OnTriggerExit(RoundStateManager manager, Collider other)
    {
        
    }

    public override void ExitState(RoundStateManager manager)
    {
        
    }
}