using System.Collections;
using UnityEngine;

public class BallThrowingTutorial : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject mouseImage;
    public GameObject clickImage;
    
    [Header("Positions")]
    public Transform startTransform;
    public Transform endTransform;
    
    [Header("Settings")]
    public float moveSpeed = 500f;
    public float flickSpeed = 1500f;
    public float flickDistance = 800f;

    private void Start()
    {
        // call this from Ball throwing state
        // we also need one for the pin placement state
        StartCoroutine(TutorialSequence());
    }

    private IEnumerator TutorialSequence()
    {
        // Setup initial state
        mouseImage.SetActive(true);
        clickImage.SetActive(false);
        mouseImage.transform.position = startTransform.position;
        
        // Move mouse image from start to end transform
        while (Vector3.Distance(mouseImage.transform.position, endTransform.position) > 0.1f)
        {
            mouseImage.transform.position = Vector3.MoveTowards(
                mouseImage.transform.position, 
                endTransform.position, 
                moveSpeed * Time.deltaTime
            );
            yield return null;
        }
        
        mouseImage.transform.position = endTransform.position;
        mouseImage.SetActive(false);
        
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
                
                mouseImage.SetActive(isShowingMouse);
                clickImage.SetActive(!isShowingMouse);
            }
            
            yield return null;
        }
        
        // User has clicked, force mouse image on to flick it upwards
        mouseImage.SetActive(true);
        clickImage.SetActive(false);
        
        Vector3 flickTarget = mouseImage.transform.position + (Vector3.up * flickDistance);
        
        while (Vector3.Distance(mouseImage.transform.position, flickTarget) > 0.1f)
        {
            mouseImage.transform.position = Vector3.MoveTowards(
                mouseImage.transform.position, 
                flickTarget, 
                flickSpeed * Time.deltaTime
            );
            yield return null;
        }
        
        // Tutorial finished, hide everything
        mouseImage.SetActive(false);
        gameObject.SetActive(false);
    }
}