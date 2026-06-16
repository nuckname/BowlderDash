using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class ThrowingBallState : RoundBaseState
{
    private struct CurvePoint
    {
        public Vector3 position;
        public int directionIndex; 
    }
    
    [Header("Ball & Throw Setup")]
    public GameObject ball;
    public LayerMask floorLayer;
    public LineRenderer lineRenderer;
    public Slider powerSlider;
    public AnimationCurve swingCurve;
    
    [Header("Throw Tuning Parameters")]
    public float forceMinSpeed = 5f;
    public float forceMaxSpeed = 20f;
    public float totalMinTime = 0.1f;
    public float totalMaxTime = 1.0f;
    [Tooltip("Extra hold time after the minimum draw duration before launch power starts tapering off")]
    public float powerFalloffDelay = 0.2f;
    public float minZ = -5f;
    public float maxZ = 15f;
    public float pullbackYScale = 0.15f;
    public float pullbackZScale = 0.2f;
    public float oppositeXScale = 2.0f;
    
    [Tooltip("Seconds the mouse can be released before the ball launches")]
    public float drawTimeTolerance = 0.5f;
    [Tooltip("Distance the mouse can be dragged backwards before launching")]
    public float drawPositionTolerance = 0.5f;
    
    // Internal state variables transferred from GrandGameManager
    private Vector3 startPos;
    private float ballHeightOffset;
    private Rigidbody ballRb;
    private List<CurvePoint> processedCurve = new List<CurvePoint>();
    private float mouseDownTime;
    private bool isDrawing = false;
    private bool isLaunched = false;
    private bool isValidDraw = false;
    private bool isFollowingCurve = false;
    private float mouseUpTimer = 0f;
    private const float jitterTolerance = 0.05f;

    public override void EnterState(RoundStateManager manager)
    {
        if (ball != null && ballRb == null)
        {
            ballRb = ball.GetComponent<Rigidbody>();
            startPos = ball.transform.position;
            ballHeightOffset = startPos.y;
        }

        ResetBall(manager);
    }

    public override void UpdateState(RoundStateManager manager)
    {
        // Cancel/Reset throw drawing on Right Click
        if (Input.GetMouseButtonDown(1))
        {
            ResetBall(manager);
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (isDrawing)
            {
                mouseUpTimer = 0f;
            }
            else
            {
                StartDrawing(manager);
            }
        }
        else if (Input.GetMouseButton(0) && isDrawing)
        {
            mouseUpTimer = 0f;
            UpdateDrawing(manager);
        }
        else if (isDrawing && !Input.GetMouseButton(0))
        {
            mouseUpTimer += Time.deltaTime;
            if (mouseUpTimer >= drawTimeTolerance)
            {
                EndDrawingAndLaunch(manager);
            }
        }
    }

    public override void FixedUpdateState(RoundStateManager manager)
    {
        if (!isLaunched || !isFollowingCurve || ballRb == null) return;
        ApplyCurvatureForce();
    }

    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    
    private void StartDrawing(RoundStateManager manager)
    {
        if (floorLayer.value == 0)
        {
            Debug.LogError("<b>[ThrowingBallState]</b> Floor Layer is not assigned! Raycasts will fail.");
        }

        manager.StopAllCoroutines();
        processedCurve.Clear();
        if (lineRenderer != null) lineRenderer.positionCount = 0;
        mouseUpTimer = 0f;

        Ray ray = manager.targetCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 10000f, floorLayer))
        {
            if (hit.point.z < minZ)
            {
                isValidDraw = true;
                isDrawing = true;
                mouseDownTime = Time.time;
                AddPoint(hit.point);
                manager.UpdateInstructionText("Drawing path... Release to bowl!");
                Debug.Log($"<b>[ThrowingBallState]</b> Started drawing at {hit.point}");
            }
            else
            {
                isValidDraw = false;
                isDrawing = false;
                manager.UpdateInstructionText("Draw from the ball!");
                Debug.LogWarning($"<b>[ThrowingBallState]</b> Clicked too far forward ({hit.point.z} >= {minZ}). Draw from behind the ball.");
            }
        }
    }
    
    private void UpdateDrawing(RoundStateManager manager)
    {
        Ray ray = manager.targetCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 10000f, floorLayer))
        {
            Vector3 point = hit.point;

            if (processedCurve.Count > 3 && point.z < processedCurve[processedCurve.Count - 1].position.z - drawPositionTolerance)
            {
                EndDrawingAndLaunch(manager);
                return;
            }

            if (point.z > maxZ) return;

            if (processedCurve.Count == 0 || point.z > processedCurve[processedCurve.Count - 1].position.z + 0.1f)
            {
                AddPoint(point);
            }
        }
        
        if (powerSlider != null && isValidDraw)
        {
            float currentDuration = Time.time - mouseDownTime;
            powerSlider.value = GetLaunchPowerNormalized(currentDuration);
        }
    }

    private void EndDrawingAndLaunch(RoundStateManager manager)
    {
        isDrawing = false;
        if (!isValidDraw || processedCurve.Count < 2) 
        {
            Debug.LogWarning($"<b>[ThrowingBallState]</b> Draw cancelled. Valid: {isValidDraw}, Points: {processedCurve.Count}");
            ResetBall(manager);
            return;
        }

        float duration = Time.time - mouseDownTime;
        float t = GetLaunchPowerNormalized(duration);
        float launchSpeed = Mathf.Lerp(forceMinSpeed, forceMaxSpeed, t);
        
        manager.UpdateInstructionText("Ball Released!");
        Debug.Log($"<b>[ThrowingBallState]</b> Launching ball with speed {launchSpeed} and {processedCurve.Count} curve points.");
        manager.StartCoroutine(ThrowSequence(manager, launchSpeed));
    }

    private float GetLaunchPowerNormalized(float drawDuration)
    {
        float taperStartTime = totalMinTime + Mathf.Max(0f, powerFalloffDelay);
        float taperEndTime = Mathf.Max(taperStartTime + 0.0001f, totalMaxTime);

        if (drawDuration <= taperStartTime) return 1f;
        return 1f - Mathf.InverseLerp(taperStartTime, taperEndTime, drawDuration);
    }

    private IEnumerator ThrowSequence(RoundStateManager manager, float speed)
    {
        isValidDraw = false;
        if (ballRb == null) yield break;

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

        float windupDuration = 0.5f;
        float elapsed = 0f;
        while (elapsed < windupDuration)
        {
            elapsed += Time.deltaTime;
            float targetT = elapsed / windupDuration;
            float curveVal = swingCurve != null ? swingCurve.Evaluate(targetT) : targetT;
            ball.transform.position = Vector3.Lerp(firstLinePosition, pullbackPos, curveVal);
            yield return null;
        }

        float distanceToStart = Vector3.Distance(pullbackPos, firstLinePosition);
        float swingForwardDuration = distanceToStart / speed;
        elapsed = 0f;
        while (elapsed < swingForwardDuration)
        {
            elapsed += Time.deltaTime;
            float targetT = elapsed / swingForwardDuration;
            float easeIn = targetT * targetT; 
            ball.transform.position = Vector3.Lerp(pullbackPos, firstLinePosition, easeIn);
            yield return null;
        }

        ball.transform.position = firstLinePosition;
        ballRb.isKinematic = false;
        ballRb.velocity = Vector3.forward * speed;
        isLaunched = true;
        isFollowingCurve = true;

        // NOTE: Switch state to your Rolling/Scoring tracking phase here if needed
        // manager.SwitchState(manager.RollingState);
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
        }
    }

    private void ResetBall(RoundStateManager manager)
    {
        manager.StopAllCoroutines();
        manager.StartCoroutine(ResetSequence(manager));
    }

    private IEnumerator ResetSequence(RoundStateManager manager)
    {
        isLaunched = false;
        isDrawing = false;
        isFollowingCurve = false;
        isValidDraw = false;
        
        if (ballRb != null)
        {
            ballRb.velocity = Vector3.zero;
            if (!ballRb.isKinematic) ballRb.angularVelocity = Vector3.zero;
            ballRb.isKinematic = true;
        }

        processedCurve.Clear();
        if (lineRenderer != null) lineRenderer.positionCount = 0;
        if (powerSlider != null) powerSlider.value = 0f;
        if (ball != null) ball.transform.position = startPos;
        
        manager.UpdateInstructionText("Draw the ball's path! Start from the ball.");
        yield return new WaitForSeconds(0.05f);

        if (ball != null)
        {
            ball.transform.position = startPos;
            if (ballRb != null)
            {
                ballRb.velocity = Vector3.zero;
                if (!ballRb.isKinematic) ballRb.angularVelocity = Vector3.zero;
                ballRb.isKinematic = true;
            }
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
}