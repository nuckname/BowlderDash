using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class bowlManyger : MonoBehaviour
{
    public GameObject ball;
    public LayerMask floorLayer;
    public Camera mainCamera;
    public LineRenderer lineRenderer;
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
    
    private const float jitterTolerance = 0.05f;

    private struct CurvePoint
    {
        public Vector3 position;
        public int directionIndex; 
    }

    void Start()
    {
        if (ball != null)
        {
            startPos = ball.transform.position;
            ballRb = ball.GetComponent<Rigidbody>();
            ballRb.isKinematic = true;
            ballHeightOffset = startPos.y;
        }

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
        }
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
        processedCurve.Clear();
        if (lineRenderer != null) lineRenderer.positionCount = 0;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, floorLayer))
        {
            if (hit.point.z < minZ)
            {
                isValidDraw = true;
                isDrawing = true;
                mouseDownTime = Time.time;
                AddPoint(hit.point);
            }
            else
            {
                isValidDraw = false;
                isDrawing = false;
            }
        }
    }

    private void UpdateDrawing()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, floorLayer))
        {
            Vector3 point = hit.point;

            if (processedCurve.Count > 0 && point.z < processedCurve[processedCurve.Count - 1].position.z)
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
        if (!isValidDraw || processedCurve.Count < 2) return;

        float duration = Time.time - mouseDownTime;
        float t = Mathf.InverseLerp(totalMinTime, totalMaxTime, duration);
        float launchSpeed = Mathf.Lerp(forceMinSpeed, forceMaxSpeed, t);

        StartCoroutine(ThrowSequence(launchSpeed));
    }

    private IEnumerator ThrowSequence(float speed)
    {
        isValidDraw = false;
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
        StopAllCoroutines();
        isLaunched = false;
        isDrawing = false;
        isFollowingCurve = false;
        ballRb.isKinematic = true;
        ballRb.velocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;
        ball.transform.position = startPos;
        processedCurve.Clear();
        if (lineRenderer != null) lineRenderer.positionCount = 0;
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
