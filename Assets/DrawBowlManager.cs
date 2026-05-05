using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DrawBowlManager : MonoBehaviour
{
    [Header("Dependencies")]
    public GameObject ball;
    public LayerMask floorLayer;
    public Camera mainCamera;
    public UnityEngine.LineRenderer lineRenderer;
    
    [Header("UI Integration")]
    public Text instructionText;
    public Slider powerSlider;
    
    [Header("Physics Curves")]
    public AnimationCurve swingCurve;

    public float forceMinSpeed = 5f;
    public float forceMaxSpeed = 20f;
    public float totalMinTime = 0.1f;
    public float totalMaxTime = 1.0f;

    public float minZ = -5f;
    public float maxZ = 15f;

    public float pullbackYScale = 0.15f;
    public float pullbackZScale = 0.2f;
    public float oppositeXScale = 2.0f;

    private Vector3 startPos;
    private float ballHeightOffset;
    private Rigidbody ballRb;
    private List<CurvePoint> processedCurve = new List<CurvePoint>();
    private float mouseDownTime;
    private bool isDrawing = false;
    private bool isLaunched = false;
    private bool isValidDraw = false;
    private bool isFollowingCurve = false;
    private bool isInitialized = false;
    
    private const float jitterTolerance = 0.05f;

    private struct CurvePoint
    {
        public Vector3 position;
        public int directionIndex; 
    }

    void Start()
    {
        EnsureInitialized();

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
        }
        
        UpdateInstruction("Draw the ball's path! Start from the ball.");
        if (powerSlider != null) powerSlider.value = 0f;
    }

    public void UpdateInstruction(string newText)
    {
        if (instructionText != null)
        {
            instructionText.text = newText;
        }
    }

    public void UpdateInstructionText(string newText)
    {
        UpdateInstruction(newText);
    }

    public void ResetThrowSetup()
    {
        EnsureInitialized();
        ResetBall();
    }

    public void StopFollowingCurve()
    {
        isFollowingCurve = false;
    }

    private void EnsureInitialized()
    {
        if (isInitialized) return;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (ball != null)
        {
            startPos = ball.transform.position;
            ballRb = ball.GetComponent<Rigidbody>();
            if (ballRb != null)
            {
                ballRb.isKinematic = true;
            }
            ballHeightOffset = startPos.y;
        }

        isInitialized = true;
    }

    void Update()
    {
        HandleInput();
        
        if (Input.GetMouseButtonDown(1))
        {
            ResetBall();
        }
    }

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            StartDrawing();
        }
        else if (Input.GetMouseButton(0) && isDrawing)
        {
            UpdateDrawing();
        }
        else if (Input.GetMouseButtonUp(0) && isDrawing)
        {
            EndDrawingAndLaunch();
        }
    }

    private void StartDrawing()
    {
        EnsureInitialized();
        StopAllCoroutines();
        processedCurve.Clear();
        if (lineRenderer != null) lineRenderer.positionCount = 0;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 10000f, floorLayer))
        {
            if (hit.point.z < minZ)
            {
                isValidDraw = true;
                isDrawing = true;
                mouseDownTime = Time.time;
                AddPoint(hit.point);
                
                UpdateInstruction("Drawing path... Release to bowl!");
            }
            else
            {
                isValidDraw = false;
                isDrawing = false;
                UpdateInstruction("Draw from the ball!");
            }
        }
        Debug.Log(isValidDraw + " " + isDrawing);
    }

    private void UpdateDrawing()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 10000f, floorLayer))
        {
            Vector3 point = hit.point;

            if (processedCurve.Count > 3 && point.z < processedCurve[processedCurve.Count - 1].position.z)
            {
                EndDrawingAndLaunch();
                return;
            }

            if (point.z > maxZ) return;

            if (processedCurve.Count == 0 || point.z > processedCurve[processedCurve.Count - 1].position.z + 0.1f)
            {
                AddPoint(point);
            }
        }
        
        // Track visual power accumulation
        if (powerSlider != null && isValidDraw)
        {
            float currentDuration = Time.time - mouseDownTime;
            float t = Mathf.InverseLerp(totalMinTime, totalMaxTime, currentDuration);
            powerSlider.value = t;
        }
    }

    private void AddPoint(Vector3 pos)
    {
        CurvePoint cp = new CurvePoint { position = pos, directionIndex = 0 };
        
        if (processedCurve.Count > 0)
        {
            CurvePoint last = processedCurve[processedCurve.Count - 1];
            float currentDelta = pos.x - last.position.x;
            int currentDirSign = (Mathf.Abs(currentDelta) > jitterTolerance) ? (int)Mathf.Sign(currentDelta) : 0;
            
            int lastDirSign = 0;
            for (int i = processedCurve.Count - 1; i >= 1; i--)
            {
                float d = processedCurve[i].position.x - processedCurve[i-1].position.x;
                if (Mathf.Abs(d) > jitterTolerance)
                {
                    lastDirSign = (int)Mathf.Sign(d);
                    break;
                }
            }

            cp.directionIndex = last.directionIndex;
            if (currentDirSign != 0 && lastDirSign != 0 && currentDirSign != lastDirSign)
            {
                cp.directionIndex++;
            }
        }

        processedCurve.Add(cp);

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = processedCurve.Count;
            lineRenderer.SetPosition(processedCurve.Count - 1, pos + Vector3.up * 0.05f);
        }
    }

    private void EndDrawingAndLaunch()
    {
        isDrawing = false;
        Debug.Log("end fucked");
        if (!isValidDraw || processedCurve.Count < 2) return;

        float duration = Time.time - mouseDownTime;
        float t = Mathf.InverseLerp(totalMinTime, totalMaxTime, duration);
        float launchSpeed = Mathf.Lerp(forceMinSpeed, forceMaxSpeed, t);
        
        UpdateInstruction("Ball Released!");

        StartCoroutine(ThrowSequence(launchSpeed));
    }

    private IEnumerator ThrowSequence(float speed)
    {
        EnsureInitialized();
        isValidDraw = false;
        if (ballRb == null)
        {
            yield break;
        }

        ballRb.isKinematic = true;

        Vector3 firstPointRaw = processedCurve[0].position;
        Vector3 firstLinePosition = new Vector3(firstPointRaw.x, ballHeightOffset, firstPointRaw.z);
        
        ball.transform.position = firstLinePosition;

        float deltaXAverage = 0;
        int sampleCount = Mathf.Min(processedCurve.Count, 5);
        if (sampleCount > 1)
        {
            deltaXAverage = (processedCurve[sampleCount - 1].position.x - processedCurve[0].position.x);
        }
        float pullbackXOffset = -deltaXAverage * oppositeXScale;

        float pullbackY = speed * pullbackYScale;
        float pullbackZ = speed * pullbackZScale;

        Vector3 pullbackPos = new Vector3(
            firstLinePosition.x + pullbackXOffset,
            firstLinePosition.y + pullbackY,
            firstLinePosition.z - pullbackZ
        );

        // 1. WIND-UP, EASE OUT FOR PULL BACK (floor -> peak)
        float windupDuration = 0.5f;
        float elapsed = 0f;
        while (elapsed < windupDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / windupDuration;
            float curveVal = swingCurve.Evaluate(t);
            ball.transform.position = Vector3.Lerp(firstLinePosition, pullbackPos, curveVal);
            yield return null;
        }

        // 2. FORWARD THROW, ONLY EASE IN
        float distanceToStart = Vector3.Distance(pullbackPos, firstLinePosition);
        float swingForwardDuration = distanceToStart / speed;
        elapsed = 0f;
        while (elapsed < swingForwardDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / swingForwardDuration;
            
            // square function (t * t) for no deceleration at release
            float easeIn = t * t; 

            ball.transform.position = Vector3.Lerp(pullbackPos, firstLinePosition, easeIn);
            yield return null;
        }

        ball.transform.position = firstLinePosition;
        ballRb.isKinematic = false;
        ballRb.velocity = Vector3.forward * speed;
        isLaunched = true;
        isFollowingCurve = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyBallRolled();
        }
    }

    void FixedUpdate()
    {
        if (!isLaunched || !isFollowingCurve) return;

        ApplyCurvatureForce();
    }

    private void ApplyCurvatureForce()
    {
        Vector3 currentPos = ball.transform.position;
        float targetX = currentPos.x;
        bool found = false;
        int currentDirectionIndex = 0;

        for (int i = 0; i < processedCurve.Count - 1; i++)
        {
            if (currentPos.z >= processedCurve[i].position.z && currentPos.z <= processedCurve[i+1].position.z)
            {
                float t = Mathf.InverseLerp(processedCurve[i].position.z, processedCurve[i+1].position.z, currentPos.z);
                targetX = Mathf.Lerp(processedCurve[i].position.x, processedCurve[i+1].position.x, t);
                currentDirectionIndex = processedCurve[i].directionIndex;
                found = true;
                break;
            }
        }

        if (currentDirectionIndex >= 2)
        {
            isFollowingCurve = false;
            return;
        }

        if (found)
        {
            float xDiff = targetX - currentPos.x;
            float forceStrength = 60f; 
            ballRb.AddForce(new Vector3(xDiff * forceStrength, 0, 0), ForceMode.Acceleration);
            Vector3 vel = ballRb.velocity;
            vel.x *= 0.85f;
            ballRb.velocity = vel;
        }
        else if (currentPos.z > maxZ)
        {
            isFollowingCurve = false;
            isLaunched = false;
        }
    }

    private void ResetBall()
    {
        EnsureInitialized();
        StopAllCoroutines();
        StartCoroutine(ResetSequence());
    }

    private IEnumerator ResetSequence()
    {
        Debug.Log("reset sequence");
        // Immediate cleanup of states to prevent other logic from running
        isLaunched = false;
        isDrawing = false;
        isFollowingCurve = false;
        
        if (ballRb != null)
        {
            ballRb.velocity = Vector3.zero;
            if (!ballRb.isKinematic)
            {
                ballRb.angularVelocity = Vector3.zero;
            }
            ballRb.isKinematic = true;
        }

        // Clear curve and UI immediately
        processedCurve.Clear();
        if (lineRenderer != null) lineRenderer.positionCount = 0;
        UpdateInstruction("Draw the ball's path! Start from the ball.");
        if (powerSlider != null) powerSlider.value = 0f;

        // First attempt at positioning
        if (ball != null) ball.transform.position = startPos;

        // Wait a small bit to ensure no other logic (like late-frame physics or other coroutines) 
        // overwrites the position during this frame's processing
        yield return new WaitForSeconds(0.05f);

        // Final positioning and state enforcement to be absolutely sure
        if (ball != null)
        {
            ball.transform.position = startPos;
            if (ballRb != null)
            {
                ballRb.velocity = Vector3.zero;
                if (!ballRb.isKinematic)
                {
                    ballRb.angularVelocity = Vector3.zero;
                }
                ballRb.isKinematic = true;
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isLaunched) return;
        if (((1 << collision.gameObject.layer) & floorLayer) == 0)
        {
            isFollowingCurve = false;
        }
    }
}
