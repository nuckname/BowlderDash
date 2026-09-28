using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// AI
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
    public string tryTryThrowMessage = "Too Slow! Try again!";
    public string pickUpBallMessage = "Pick up ball here";

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
    
    [Header("Pick Up Ball Position")]
    public Transform pickUpBallTextTransform;
    public Transform pickUpBallStartTransform;
    
    [Header("Settings")]
    public bool enableTutorial = true;
    public float moveSpeed = 500f;
    public float flickSpeed = 1500f;
    public float flickDistance = 800f;

    [Header("Interaction Areas")]
    public RectTransform stopAreaBox;
    public Transform buttonTransform;
    public float delayBeforeShowButton = 5f;
    
    private List<GameObject> activeTutorialImages = new List<GameObject>();
    private Coroutine currentSequence;

    private enum TutorialState { None, Throw, PinPlacement, ShowButtonToThrow, ShowWhereToPickUpBall }
    [SerializeField] private TutorialState currentState = TutorialState.None;

    [SerializeField] private RoundStateManager roundStateManager;

    private void Update()
    {
        if (currentState != TutorialState.None && Input.GetMouseButtonDown(0))
        {
            if (currentState == TutorialState.ShowButtonToThrow)
            {
                if (buttonTransform != null)
                {
                    RectTransform buttonRect = buttonTransform as RectTransform;
                    if (buttonRect != null && RectTransformUtility.RectangleContainsScreenPoint(buttonRect, Input.mousePosition, null))
                    {
                        Debug.Log("Button clicked! Transitioning to Throw tutorial.");
                        PlayThrowTutorial(); 
                    }
                }
            }
            else if (stopAreaBox != null && RectTransformUtility.RectangleContainsScreenPoint(stopAreaBox, Input.mousePosition, null))
            {
                if (currentState == TutorialState.PinPlacement)
                {
                    StopTutorial();
                    
                    if (roundStateManager.currentState == roundStateManager.throwingBallState)
                    {
                        // Play the throw tutorial directly and update the state
                        PlayThrowTutorial(); 
                    }
                    else
                    {
                        // Assign this to currentSequence so it can be canceled by StopTutorial()
                        currentSequence = StartCoroutine(DelayedShowButtonToThrow());
                    }
                }
                else if (currentState != TutorialState.Throw) 
                {
                    StopTutorial();
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
        if (!enableTutorial) return;
        
        StopTutorial();
        currentState = TutorialState.Throw;
        currentSequence = StartCoroutine(ThrowTutorialSequence());
    }
    
    public void PlayTryThrowBallAgainTutorial()
    {
        if (!enableTutorial) return;
        
        StopTutorial();
        currentState = TutorialState.Throw; // Set to throw state so clicks are handled properly
        currentSequence = StartCoroutine(ShowPlayTryThrowBallAgainText());
    }

    private IEnumerator ShowPlayTryThrowBallAgainText()
    {
        SpawnText(textBoxPrefab,
            pinTextTransform.position,
            tryTryThrowMessage);

        // Wait for the extracted mouse click and flick sequence instead of just 8 seconds
        yield return StartCoroutine(MouseFlickSequence(throwStartTransform));

        StopTutorial();
    }

    public void PlayPinPlacementTutorial()
    {
        if (!enableTutorial) return;
        
        StopTutorial();
        currentState = TutorialState.PinPlacement;
        currentSequence = StartCoroutine(PinPlacementSequence());
    }

    public void ShowButtonToBallThrow()
    {
        if (!enableTutorial) return;
        
        StopTutorial();
        currentState = TutorialState.ShowButtonToThrow;
        currentSequence = StartCoroutine(ClickToThrowPinTutorial());
    }

    public void PlayPickUpBallTutorial()
    {
        if (!enableTutorial) return;
        
        StopTutorial();
        currentState = TutorialState.ShowWhereToPickUpBall;
        currentSequence = StartCoroutine(PickUpBallTutorialSequence());
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

    public GameObject SpawnText(GameObject prefab, Vector3 position, string message)
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
        
        // Use the extracted sequence
        yield return StartCoroutine(MouseFlickSequence(throwStartTransform));
        
        StopTutorial();
    }
    
// Extracted the reusable mouse flashing and flick logic into its own Coroutine
    private IEnumerator MouseFlickSequence(Transform startTransform)
    {
        // Spawn both images at the starting position
        GameObject mouseInstance = SpawnImage(mouseImagePrefab, startTransform.position);
        GameObject clickInstance = SpawnImage(clickImagePrefab, startTransform.position);

        // Initial setup
        mouseInstance.SetActive(false);
        clickInstance.SetActive(true);

        // Flash between mouse and click image every second until user clicks
        float flashTimer = 0f;
        bool isShowingMouse = false;

        while (!Input.GetMouseButtonDown(0))
        {
            if (mouseInstance == null || clickInstance == null) yield break;

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

        if (mouseInstance == null || clickInstance == null) yield break;

        // User has clicked, force mouse image on to flick it upwards
        mouseInstance.SetActive(true);
        clickInstance.SetActive(false);

        Vector3 flickTarget = mouseInstance.transform.position + (Vector3.up * flickDistance);

        while (mouseInstance != null && Vector3.Distance(mouseInstance.transform.position, flickTarget) > 0.1f)
        {
            mouseInstance.transform.position = Vector3.MoveTowards(
                mouseInstance.transform.position, 
                flickTarget, 
                flickSpeed * Time.deltaTime
            );
            yield return null;
        }
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

    private IEnumerator PickUpBallTutorialSequence()
    {
        SpawnText(textBoxPrefab, pickUpBallTextTransform.position, pickUpBallMessage);
        
        GameObject mouseInstance = SpawnImage(mouseImagePrefab, pickUpBallStartTransform.position);
        GameObject clickInstance = SpawnImage(clickImagePrefab, pickUpBallStartTransform.position);
        
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