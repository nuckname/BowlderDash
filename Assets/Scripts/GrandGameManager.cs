using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public enum GameState { FrameSetup, PinPlacement, PlayerReady, Drawing, Rolling, CalculatingScore, EndFrame, GameOver }

[RequireComponent(typeof(PinHandInventory))]
public class GrandGameManager : MonoBehaviour
{
    public static GrandGameManager Instance { get; private set; }

    [Header("Core Dependencies")]
    public ScoreCalculator scoreCalculator;
    public GameUIController uiController;
    public PinHandInventory pinHandInventory;
    public HexGridSnapper grid;

    [Header("Game State")]
    public int maxFrames = 10;
    public int currentFrame = 1;
    public int throwsThisFrame = 0;
    public int totalScore = 0;

    [Header("Ball & Throw Setup")]
    public GameObject ball;
    public LayerMask floorLayer;
    public LineRenderer lineRenderer;
    public Text instructionText;
    public Slider powerSlider;
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
    
    [Tooltip("Seconds the mouse can be released before the ball launches")]
    public float drawTimeTolerance = 0.5f;
    [Tooltip("Distance the mouse can be dragged backwards before launching")]
    public float drawPositionTolerance = 0.5f;

    [Header("Camera Transitions")]
    public Camera targetCamera;
    public Transform placementCameraTarget;
    public Transform cameraFinalTransform; // End of lane transition target
    public float cameraSmoothSpeed = 0.125f;
    public float cameraTransitionDistance = 10f;
    public float cameraTransitionSmoothness = 0.5f;
    public float cameraReturnSmoothness = 5f;
    public float cameraPlacementTransitionTime = 1f;

    private Vector3 cameraOffset;
    private Quaternion cameraInitialRotation;
    private float currentCameraTransitionT = 0f;
    private float placementBlend = 0f;

    // Game state
    private GameState _currentState;
    public GameState GetCurrentState() => _currentState;

    private Pin[] _allPinsPool;
    private exploder[] _laneExploders;

    // Throwing internal state
    private struct CurvePoint
    {
        public Vector3 position;
        public int directionIndex; 
    }
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

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnValidate()
    {
        if (pinHandInventory == null)
            pinHandInventory = GetComponent<PinHandInventory>();
    }

    void Start()
    {
        pinHandInventory = GetComponent<PinHandInventory>();
        if (pinHandInventory == null)
            pinHandInventory = gameObject.AddComponent<PinHandInventory>();

        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera != null && ball != null)
        {
            cameraOffset = targetCamera.transform.position - ball.transform.position;
            cameraInitialRotation = targetCamera.transform.rotation;
        }

        if (ball != null)
        {
            startPos = ball.transform.position;
            ballRb = ball.GetComponent<Rigidbody>();
            if (ballRb != null) ballRb.isKinematic = true;
            ballHeightOffset = startPos.y;
        }

        if (lineRenderer != null) lineRenderer.positionCount = 0;
        if (powerSlider != null) powerSlider.value = 0f;

        pinHandInventory.Configure(this, grid, null); // passing null for modeSwitcher since it's removed
        
        RefreshRuntimePools();
        StartNewGame();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("Resetting Game...");
            StartNewGame();
        }

        HandleInput();
        UpdateCamera();
    }

    void FixedUpdate()
    {
        if (!isLaunched || !isFollowingCurve) return;
        ApplyCurvatureForce();
    }

    // --- GAME FLOW LOGIC ---

    public void StartNewGame()
    {
        totalScore = 0;
        currentFrame = 1;
        StartFrame();
    }

    private void StartFrame()
    {
        _currentState = GameState.FrameSetup;
        throwsThisFrame = 0;

        if (scoreCalculator != null) scoreCalculator.ResetFrameScore();

        Debug.Log("Current State: Frame Setup");

        pinHandInventory?.ResetInventoryAndPins();
        RefreshRuntimePools();
        
        foreach(var p in _allPinsPool) 
        {
            if (p.gameObject.scene.isLoaded)
            {
                p.gameObject.SetActive(true);
                p.ResetPin();
            }
        }

        if (uiController != null) uiController.UpdateStandardUI(this);
        pinHandInventory?.PrepareForPlacementPhase();

        EnterPinPlacement();
    }

    private void EnterPinPlacement()
    {
        _currentState = GameState.PinPlacement;
        Debug.Log("Current State: Pin Placement");

        TogglePinsPhysics(false);
        ResetBall();
        
        if (grid != null) grid.showGizmos = true;
        UpdateInstructionText("Click and drag to arrange pins. Press TAB when ready.");
        
        if (uiController != null) uiController.UpdateStandardUI(this);
        pinHandInventory?.PrepareForPlacementPhase();
    }

    private void ReadyPlayer()
    {
        _currentState = GameState.PlayerReady;
        Debug.Log("Current State: Player Ready");

        TogglePinsPhysics(true);
        if (grid != null) grid.showGizmos = false;
        
        ResetBall();
        UpdateInstructionText("Draw the ball's path! Start from the ball.");
        
        if (uiController != null) uiController.UpdateStandardUI(this);
        Debug.Log($"Frame {currentFrame} Throw {throwsThisFrame + 1}: Player Ready.");
    }

    private void TogglePinsPhysics(bool enable)
    {
        RefreshRuntimePools();
        foreach (var pin in _allPinsPool)
        {
            if (pin.gameObject.scene.isLoaded)
            {
                pin.SetPhysicsEnabled(enable);
            }
        }
    }

    private void ClearFallenPins()
    {
        RefreshRuntimePools();
        foreach(var p in _allPinsPool)
        {
            if (p.gameObject.scene.isLoaded && p.IsFallen()) 
            {
                p.gameObject.SetActive(false);
            }
        }
    }

    public void NotifyBallRolled()
    {
        _currentState = GameState.Rolling;
        throwsThisFrame++;
        Debug.Log("Current State: Rolling");
        StartCoroutine(WaitAndScore());
    }

    private IEnumerator WaitAndScore()
    {
        yield return new WaitForSeconds(6f);

        _currentState = GameState.CalculatingScore;
        Debug.Log("Current State: Calculating Score");

        int totalPinsActiveAtStart = 0;
        int fallenPinsThisThrow = 0;

        RefreshRuntimePools();
        foreach(var p in _allPinsPool)
        {
            if (p.gameObject.scene.isLoaded && p.gameObject.activeInHierarchy)
            {
                totalPinsActiveAtStart++;
                if (p.IsFallen()) fallenPinsThisThrow++;
            }
        }

        bool isStrike = (throwsThisFrame == 1 && fallenPinsThisThrow == totalPinsActiveAtStart && totalPinsActiveAtStart > 0);

        if (isStrike && scoreCalculator != null)
        {
            scoreCalculator.ApplyStrikeBonus();
        }

        int frameScore = 0;
        if (scoreCalculator != null) frameScore = scoreCalculator.GetFinalScore();

        totalScore += frameScore;
        Debug.Log($"Tallying Frame! Points Earned: {frameScore}. Total: {totalScore}");
        
        if (uiController != null) uiController.UpdateStandardUI(this);

        // TODO: UI Scoring feedback script goes here later

        if (throwsThisFrame >= 2 || isStrike)
        {
            currentFrame++;
            if (currentFrame > maxFrames)
            {
                _currentState = GameState.GameOver;
                Debug.Log($"Game Over! Final Score: {totalScore}");
            }
            else
            {
                StartFrame();
            }
        }
        else
        {
            ClearFallenPins();
            ReadyPlayer();
        }
    }

    private void RefreshRuntimePools()
    {
        _allPinsPool = FindObjectsOfType<Pin>(true);
        _laneExploders = FindObjectsOfType<exploder>(true);
    }

    // --- INPUT & THROWING LOGIC ---

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (throwsThisFrame == 0 && (_currentState == GameState.PinPlacement || _currentState == GameState.PlayerReady))
            {
                if (_currentState == GameState.PinPlacement) ReadyPlayer();
                else EnterPinPlacement();
            }
        }

        if (Input.GetMouseButtonDown(1) && (_currentState == GameState.PlayerReady || _currentState == GameState.Drawing))
        {
            ResetBall();
        }

        if (_currentState == GameState.PlayerReady || _currentState == GameState.Drawing)
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (isDrawing)
                {
                    // Resumed clicking within the tolerance window
                    mouseUpTimer = 0f;
                }
                else
                {
                    StartDrawing();
                }
            }
            else if (Input.GetMouseButton(0) && isDrawing)
            {
                mouseUpTimer = 0f;
                UpdateDrawing();
            }
            else if (isDrawing && !Input.GetMouseButton(0))
            {
                mouseUpTimer += Time.deltaTime;
                if (mouseUpTimer >= drawTimeTolerance)
                {
                    EndDrawingAndLaunch();
                }
            }
        }
    }

    private void StartDrawing()
    {
        if (floorLayer.value == 0)
        {
            Debug.LogError("<b>[GrandGameManager]</b> Floor Layer is not assigned! Raycasts will fail.");
        }

        StopAllCoroutines();
        processedCurve.Clear();
        if (lineRenderer != null) lineRenderer.positionCount = 0;
        mouseUpTimer = 0f;

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 10000f, floorLayer))
        {
            if (hit.point.z < minZ)
            {
                _currentState = GameState.Drawing;
                isValidDraw = true;
                isDrawing = true;
                mouseDownTime = Time.time;
                AddPoint(hit.point);
                UpdateInstructionText("Drawing path... Release to bowl!");
                Debug.Log($"<b>[GrandGameManager]</b> Started drawing at {hit.point}");
            }
            else
            {
                isValidDraw = false;
                isDrawing = false;
                UpdateInstructionText("Draw from the ball!");
                Debug.LogWarning($"<b>[GrandGameManager]</b> Clicked too far forward ({hit.point.z} >= {minZ}). Draw from behind the ball.");
            }
        }
        else
        {
            Debug.LogWarning("<b>[GrandGameManager]</b> Raycast hit nothing. Are you clicking on the floorLayer?");
        }
    }

    private void UpdateDrawing()
    {
        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 10000f, floorLayer))
        {
            Vector3 point = hit.point;

            // Position tolerance: if dragged back beyond tolerance, launch
            if (processedCurve.Count > 3 && point.z < processedCurve[processedCurve.Count - 1].position.z - drawPositionTolerance)
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
        if (!isValidDraw || processedCurve.Count < 2) 
        {
            Debug.LogWarning($"<b>[GrandGameManager]</b> Draw cancelled. Valid: {isValidDraw}, Points: {processedCurve.Count}");
            if (_currentState == GameState.Drawing) _currentState = GameState.PlayerReady;
            return;
        }

        float duration = Time.time - mouseDownTime;
        float t = Mathf.InverseLerp(totalMinTime, totalMaxTime, duration);
        float launchSpeed = Mathf.Lerp(forceMinSpeed, forceMaxSpeed, t);
        
        UpdateInstructionText("Ball Released!");
        _currentState = GameState.Rolling; // Instantly lock out further drawing
        Debug.Log($"<b>[GrandGameManager]</b> Launching ball with speed {launchSpeed} and {processedCurve.Count} curve points.");
        StartCoroutine(ThrowSequence(launchSpeed));
    }

    private IEnumerator ThrowSequence(float speed)
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
            float t = elapsed / windupDuration;
            float curveVal = swingCurve != null ? swingCurve.Evaluate(t) : t;
            ball.transform.position = Vector3.Lerp(firstLinePosition, pullbackPos, curveVal);
            yield return null;
        }

        float distanceToStart = Vector3.Distance(pullbackPos, firstLinePosition);
        float swingForwardDuration = distanceToStart / speed;
        elapsed = 0f;
        while (elapsed < swingForwardDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / swingForwardDuration;
            float easeIn = t * t; 
            ball.transform.position = Vector3.Lerp(pullbackPos, firstLinePosition, easeIn);
            yield return null;
        }

        ball.transform.position = firstLinePosition;
        ballRb.isKinematic = false;
        ballRb.velocity = Vector3.forward * speed;
        isLaunched = true;
        isFollowingCurve = true;

        NotifyBallRolled();
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

    private void ResetBall()
    {
        StopAllCoroutines();
        StartCoroutine(ResetSequence());
    }

    private IEnumerator ResetSequence()
    {
        isLaunched = false;
        isDrawing = false;
        isFollowingCurve = false;
        
        if (_currentState == GameState.Drawing)
        {
            _currentState = GameState.PlayerReady;
        }
        
        if (ballRb != null)
        {
            ballRb.velocity = Vector3.zero;
            if (!ballRb.isKinematic) ballRb.angularVelocity = Vector3.zero;
            ballRb.isKinematic = true;
        }

        processedCurve.Clear();
        if (lineRenderer != null) lineRenderer.positionCount = 0;
        
        if (_currentState == GameState.PlayerReady)
            UpdateInstructionText("Draw the ball's path! Start from the ball.");
        if (powerSlider != null) powerSlider.value = 0f;

        if (ball != null) ball.transform.position = startPos;
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

    public void UpdateInstructionText(string newText)
    {
        if (instructionText != null) instructionText.text = newText;
    }

    public void StopFollowingCurve()
    {
        isFollowingCurve = false;
    }

    // --- CAMERA LOGIC ---

    private void UpdateCamera()
    {
        if (targetCamera == null) return;

        bool placementModeActive = (_currentState == GameState.FrameSetup || _currentState == GameState.PinPlacement);
        float targetPlacementBlend = (placementModeActive && placementCameraTarget != null) ? 1f : 0f;
        
        if (!Mathf.Approximately(placementBlend, targetPlacementBlend))
        {
            float duration = Mathf.Max(0.0001f, cameraPlacementTransitionTime);
            placementBlend = Mathf.MoveTowards(placementBlend, targetPlacementBlend, Time.deltaTime / duration);
        }

        Vector3 followPosition = targetCamera.transform.position;
        Quaternion followRotation = targetCamera.transform.rotation;

        if (ball != null)
        {
            GetFollowPose(out followPosition, out followRotation);
        }

        Vector3 desiredPosition = followPosition;
        Quaternion desiredRotation = followRotation;

        if (placementCameraTarget != null && placementBlend > 0f)
        {
            float smoothPlacementBlend = placementBlend * placementBlend * (3f - 2f * placementBlend);
            desiredPosition = Vector3.Lerp(followPosition, placementCameraTarget.position, smoothPlacementBlend);
            desiredRotation = Quaternion.Slerp(followRotation, placementCameraTarget.rotation, smoothPlacementBlend);
        }

        targetCamera.transform.position = Vector3.Lerp(targetCamera.transform.position, desiredPosition, cameraSmoothSpeed);
        targetCamera.transform.rotation = Quaternion.Slerp(targetCamera.transform.rotation, desiredRotation, cameraSmoothSpeed);
    }

    private void GetFollowPose(out Vector3 position, out Quaternion rotation)
    {
        Vector3 followPosition = new Vector3(targetCamera.transform.position.x, cameraOffset.y, ball.transform.position.z + cameraOffset.z);
        position = followPosition;
        rotation = cameraInitialRotation;

        if (cameraFinalTransform != null)
        {
            float distRemaining = cameraFinalTransform.position.z - followPosition.z;
            float targetT = 1f - Mathf.Clamp01(distRemaining / cameraTransitionDistance);

            bool isReturning = targetT < currentCameraTransitionT;
            float speed = isReturning ? cameraReturnSmoothness : cameraTransitionSmoothness;
            currentCameraTransitionT = Mathf.Lerp(currentCameraTransitionT, targetT, speed * Time.deltaTime);

            float smoothT = isReturning ? currentCameraTransitionT : currentCameraTransitionT * currentCameraTransitionT * (3f - 2f * currentCameraTransitionT);
            position = Vector3.Lerp(followPosition, cameraFinalTransform.position, smoothT);
            rotation = Quaternion.Slerp(cameraInitialRotation, cameraFinalTransform.rotation, smoothT);
        }
    }
}
