using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    [Header("UI Prefabs")]
    public GameObject mouseImagePrefab;
    public GameObject clickImagePrefab;
    public GameObject textBoxPrefab;
    public Transform canvasParent;
    
    public float textTypeDelay = 0.05f;
    
    [Header("Text Content")]
    public string throwTutorialMessage = "Click and drag to draw a path";
    public string pinPlacementMessage = "Drag pins to place them";
    public string clickToThrowMessage = "Click Here";

    [Header("Text Positions")]
    public Transform throwTextTransform;
    public Transform pinTextTransform;
    
    [Header("Throw Positions")]
    public Transform throwStartTransform;
    public Transform throwEndTransform;
    
    [Header("Pin Positions")]
    public Transform pinStartTransform;
    public Transform pinEndTransform;
    
    [Header("Click Button Position")]
    public Transform clickToThrowTextTransform;
    public Transform clickToThrowStartTransform;
    
    [Header("Settings")]
    public float moveSpeed = 500f;
    public float flickSpeed = 1500f;
    public float flickDistance = 800f;

    [Header("Interaction Areas")]
    public RectTransform stopAreaBox;
    public Transform buttonTransform;
    public float delayBeforeShowButton = 5f;
    
    private List<GameObject> activeTutorialImages = new List<GameObject>();
    private Coroutine currentSequence;

    private enum TutorialState { None, Throw, PinPlacement, ShowButtonToThrow }
    private TutorialState currentState = TutorialState.None;

    private void Update()
    {
        if (currentState != TutorialState.None && Input.GetMouseButtonDown(0))
        {
            if (stopAreaBox != null && RectTransformUtility.RectangleContainsScreenPoint(stopAreaBox, Input.mousePosition, null))
            {
                if (currentState == TutorialState.PinPlacement)
                {
                    Debug.Log("Stop area clicked! Transitioning from PinPlacement to ShowButtonToThrow.");
                    StopTutorial();
                    StartCoroutine(DelayedShowButtonToThrow());
                }
                else
                {
                    StopTutorial();
                }
            }

            if (buttonTransform != null && currentState == TutorialState.ShowButtonToThrow)
            {
                RectTransform buttonRect = buttonTransform as RectTransform;
                if (buttonRect != null && RectTransformUtility.RectangleContainsScreenPoint(buttonRect, Input.mousePosition, null))
                {
                    Debug.Log("Button clicked! Transitioning to Throw tutorial.");
                    PlayThrowTutorial(); 
                }
            }
        }
    }
    
    private IEnumerator DelayedShowButtonToThrow()
    {
        yield return new WaitForSeconds(delayBeforeShowButton);
        ShowButtonToBallThrow();
    }
    
    public void PlayThrowTutorial()
    {
        StopTutorial();
        currentState = TutorialState.Throw;
        currentSequence = StartCoroutine(ThrowTutorialSequence());
    }

    public void PlayPinPlacementTutorial()
    {
        StopTutorial();
        currentState = TutorialState.PinPlacement;
        currentSequence = StartCoroutine(PinPlacementSequence());
    }

    public void ShowButtonToBallThrow()
    {
        StopTutorial();
        currentState = TutorialState.ShowButtonToThrow;
        currentSequence = StartCoroutine(ClickToThrowPinTutorial());
    }

    public void StopTutorial()
    {
        currentState = TutorialState.None;

        if (currentSequence != null)
        {
            StopCoroutine(currentSequence);
            currentSequence = null;
        }

        foreach (GameObject img in activeTutorialImages)
        {
            if (img != null) Destroy(img);
        }
        activeTutorialImages.Clear();
    }

    private GameObject SpawnImage(GameObject prefab, Vector3 position)
    {
        GameObject instance = Instantiate(prefab, position, Quaternion.identity, canvasParent);
        activeTutorialImages.Add(instance);
        return instance;
    }

    private GameObject SpawnText(GameObject prefab, Vector3 position, string message)
    {
        GameObject instance = Instantiate(prefab, position, Quaternion.identity, canvasParent);
        
        TextMeshProUGUI tmpText = instance.GetComponent<TextMeshProUGUI>() ?? instance.GetComponentInChildren<TextMeshProUGUI>();
        Text uiText = instance.GetComponent<Text>() ?? instance.GetComponentInChildren<Text>();
        
        RectTransform boxRect = instance.GetComponent<RectTransform>();

        if (tmpText != null)
        {
            tmpText.text = "";
            StartCoroutine(TypeTextRoutine(tmpText, null, message, boxRect));
        }
        else if (uiText != null)
        {
            uiText.text = "";
            StartCoroutine(TypeTextRoutine(null, uiText, message, boxRect));
        }

        activeTutorialImages.Add(instance);
        return instance;
    }

    private IEnumerator TypeTextRoutine(TextMeshProUGUI tmpText, Text uiText, string message, RectTransform boxRect)
    {
        for (int i = 0; i <= message.Length; i++)
        {
            string currentText = message.Substring(0, i);
            
            if (tmpText != null)
            {
                tmpText.text = currentText;
            }
            else if (uiText != null)
            {
                uiText.text = currentText;
            }
            
            if (boxRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(boxRect);
            }
            
            yield return new WaitForSeconds(textTypeDelay);
        }
    }
    
    private IEnumerator TypeTextRoutine(TextMeshProUGUI tmpText, Text uiText, string message)
    {
        for (int i = 0; i <= message.Length; i++)
        {
            string currentText = message.Substring(0, i);
            
            if (tmpText != null)
            {
                tmpText.text = currentText;
            }
            else if (uiText != null)
            {
                uiText.text = currentText;
            }
            
            yield return new WaitForSeconds(textTypeDelay);
        }
    }
    
    private IEnumerator ThrowTutorialSequence()
    {
        SpawnText(textBoxPrefab, throwTextTransform.position, throwTutorialMessage);
        GameObject mouseInstance = SpawnImage(mouseImagePrefab, throwStartTransform.position);
        GameObject clickInstance = SpawnImage(clickImagePrefab, throwStartTransform.position);
        clickInstance.SetActive(false);
        
        // Move mouse image from start to end transform
        while (Vector3.Distance(mouseInstance.transform.position, throwEndTransform.position) > 0.1f)
        {
            mouseInstance.transform.position = Vector3.MoveTowards(
                mouseInstance.transform.position, 
                throwEndTransform.position, 
                moveSpeed * Time.deltaTime
            );
            yield return null;
        }
        
        mouseInstance.transform.position = throwEndTransform.position;
        mouseInstance.SetActive(false);
        
        // Flash between mouse and click image every second until user clicks
        float flashTimer = 0f;
        bool isShowingMouse = false;
        
        while (!Input.GetMouseButtonDown(0))
        {
            flashTimer += Time.deltaTime;
            
            if (flashTimer >= 1f)
            {
                flashTimer = 0f;
                isShowingMouse = !isShowingMouse;
                
                mouseInstance.SetActive(isShowingMouse);
                clickInstance.SetActive(!isShowingMouse);
                
                clickInstance.transform.position = mouseInstance.transform.position;
            }
            
            yield return null;
        }
        
        // User has clicked, force mouse image on to flick it upwards
        mouseInstance.SetActive(true);
        clickInstance.SetActive(false);
        
        Vector3 flickTarget = mouseInstance.transform.position + (Vector3.up * flickDistance);
        
        while (Vector3.Distance(mouseInstance.transform.position, flickTarget) > 0.1f)
        {
            mouseInstance.transform.position = Vector3.MoveTowards(
                mouseInstance.transform.position, 
                flickTarget, 
                flickSpeed * Time.deltaTime
            );
            yield return null;
        }
        
        StopTutorial();
    }

    private IEnumerator PinPlacementSequence()
    {
        if (pinStartTransform == null || pinEndTransform == null)
        {
            Debug.LogError("Pin Start or End Transform is missing in the Inspector!");
            yield break;
        }

        SpawnText(textBoxPrefab, pinTextTransform.position, pinPlacementMessage);
        GameObject mouseInstance = SpawnImage(mouseImagePrefab, pinStartTransform.position);
        GameObject clickInstance = SpawnImage(clickImagePrefab, pinStartTransform.position);
        
        clickInstance.SetActive(false);
        mouseInstance.SetActive(true); // Force the mouse active just in case

        Debug.Log($"Playing Pin Placement Sequence. Moving from {pinStartTransform.position} to {pinEndTransform.position}");

        while (true)
        {
            mouseInstance.transform.position = pinStartTransform.position;
            
            while (Vector3.Distance(mouseInstance.transform.position, pinEndTransform.position) > 0.1f)
            {
                mouseInstance.transform.position = Vector3.MoveTowards(
                    mouseInstance.transform.position, 
                    pinEndTransform.position, 
                    moveSpeed * Time.deltaTime
                );
                yield return null;
            }
            
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    private IEnumerator ClickToThrowPinTutorial()
    {
        SpawnText(textBoxPrefab, clickToThrowTextTransform.position, clickToThrowMessage);
        
        GameObject mouseInstance = SpawnImage(mouseImagePrefab, clickToThrowStartTransform.position);
        GameObject clickInstance = SpawnImage(clickImagePrefab, clickToThrowStartTransform.position);
        
        mouseInstance.SetActive(false);
        clickInstance.SetActive(true);

        float flashTimer = 0f;
        bool isShowingMouse = false;

        while (true)
        {
            flashTimer += Time.deltaTime;
            
            if (flashTimer >= 0.5f)
            {
                flashTimer = 0f;
                isShowingMouse = !isShowingMouse;
                
                mouseInstance.SetActive(isShowingMouse);
                clickInstance.SetActive(!isShowingMouse);
            }
            
            yield return null;
        }
    }
}