using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public enum GameState
{
    FrameSetup, 
    PinPlacement, 
    PlayerReady, 
    Drawing, 
    Rolling, 
    CalculatingScore, 
    Shop, 
    EndFrame, 
    GameOver
}

[RequireComponent(typeof(PinHandInventory))]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Core Dependencies")]
    public ScoreCalculator scoreCalculator;
    public GameUIController uiController;
    public PinHandInventory pinHandInventory;
    public PinShopController shopController;
    public HexGridSnapper grid;

    [Header("Game State")]
    public int maxFrames = 10;
    public int maxThrowsPerFrame = 2;
    public int currentFrame = 1;
    public int throwsThisFrame = 0;
    public int totalScore = 0;

    [Header("Ticket Economy")]
    public int startingTickets = 0;
    public int currentTickets = 0;
    public int gridTicketsEarnedThisFrame = 0;
    public int scoreTargetTicketsEarnedThisFrame = 0;
    public int ticketsEarnedLastFrame = 0;
    public bool lastFrameUsedTicketBonus = false;
    public bool lastFrameUsedStrikeTicketBonus = false;

    [Header("Frame Score Targets")]
    public int baseFrameScoreTarget = 30;
    public int flatFrameScoreTargetBonus = 0;
    public float frameScoreTargetGrowthScale = 1f;
    public float strikeTicketMultiplier = 2f;
    public int currentFrameScoreTarget = 30;
    public AnimationCurve frameScoreTargetCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.45f, 2f),
        new Keyframe(0.8f, 3.8f),
        new Keyframe(1f, 6f)
    );

    [Header("Exploder Settings")]
    public bool resetExploderDeformationEachFrame = true;

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

    [Header("Camera Transitions")]
    public Camera targetCamera;
    public Transform placementCameraTarget;
    public Transform cameraFinalTransform; // End of lane transition target
    public float cameraSmoothSpeed = 0.125f;
    public float cameraTransitionDistance = 10f;
    public float cameraTransitionSmoothness = 0.5f;
    public float cameraReturnSmoothness = 5f;
    public float cameraPlacementTransitionTime = 1f;
    public Transform shopCameraTarget;
    public float cameraShopTransitionTime = 1f;

    private Vector3 cameraOffset;
    private Quaternion cameraInitialRotation;
    private float currentCameraTransitionT = 0f;
    private float placementBlend = 0f;
    private float shopBlend = 0f;

    // Game state
    private GameState _currentState;
    public GameState GetCurrentState() => _currentState;

    [SerializeField] private Pin[] allPinsPool;
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
    private int scoreAppliedToTotalThisFrame = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnValidate()
    {
        if (pinHandInventory == null)
            pinHandInventory = GetComponent<PinHandInventory>();
        if (shopController == null)
            shopController = GetComponent<PinShopController>();
    }

    private void OnEnable()
    {
        //Pin.OnPinFallenGlobal += HandlePinFallenForTickets;
    }

    private void OnDisable()
    {
        //Pin.OnPinFallenGlobal -= HandlePinFallenForTickets;
    }

    void Start()
    {
        targetCamera = Camera.main;
        
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
       // if (powerSlider != null) powerSlider.value = 0f;

        pinHandInventory.Configure(this, grid, null); // passing null for modeSwitcher since it's removed
        shopController.Configure(this, pinHandInventory);
        
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
        scoreAppliedToTotalThisFrame = 0;
        currentFrame = 1;
        currentTickets = startingTickets;
        gridTicketsEarnedThisFrame = 0;
        scoreTargetTicketsEarnedThisFrame = 0;
        ticketsEarnedLastFrame = 0;
        lastFrameUsedTicketBonus = false;
        lastFrameUsedStrikeTicketBonus = false;
        pinHandInventory?.InitializeOwnedInventoryForNewGame();
        pinHandInventory?.ClearSavedLayout();
        shopController?.HideShop();
        StartFrame();
    }

    private void StartFrame()
    {
        _currentState = GameState.FrameSetup;
        throwsThisFrame = 0;
        gridTicketsEarnedThisFrame = 0;
        scoreTargetTicketsEarnedThisFrame = 0;
        ticketsEarnedLastFrame = 0;
        lastFrameUsedTicketBonus = false;
        lastFrameUsedStrikeTicketBonus = false;
        scoreAppliedToTotalThisFrame = 0;
        currentFrameScoreTarget = CalculateFrameScoreTarget(currentFrame);

        if (scoreCalculator != null) scoreCalculator.ResetFrameScore();

        Debug.Log("Current State: Frame Setup");

        ResetLaneExploders();
        pinHandInventory?.ResetInventoryAndPins();
        shopController?.HideShop();
        RefreshRuntimePools();
        grid?.ClearAllOccupants();
        grid?.SpawnTicketsForFrame();

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
        uiController?.SetPlacementWarning(string.Empty);
        UpdateInstructionText("Drag pin cards onto the grid, then press TAB when ready." +
            "To earn tickets, drag pins onto them and hit them over!");


        if (uiController != null) uiController.UpdateStandardUI(this);
        pinHandInventory?.PrepareForPlacementPhase();
    }

    private void EnterShopPhase()
    {
        _currentState = GameState.Shop;
        Debug.Log("Current State: Shop");

        TogglePinsPhysics(false);
        ResetBall();

        if (grid != null)
        {
            grid.showGizmos = false;
            grid.ClearTickets();
        }

        UpdateInstructionText("Shop phase. Spend tickets, then continue to the next frame.");
        if (uiController != null) uiController.UpdateStandardUI(this);
        shopController?.ShowShop();
    }

    private void ReadyPlayer()
    {
        if (throwsThisFrame == 0)
        {
            pinHandInventory?.SaveCurrentLayoutAsLastUsed();
        }

        _currentState = GameState.PlayerReady;
        Debug.Log("Current State: Player Ready");

        ResetVaseThrowState();
        TogglePinsPhysics(true);
        if (grid != null) grid.showGizmos = false;
        
        ResetBall();
        UpdateInstructionText("Draw the ball's path! Left click to draw on the ground. Keep in mind the shorter you hold, the more powerful your shot!");
        
        if (uiController != null) uiController.UpdateStandardUI(this);
        Debug.Log($"Frame {currentFrame} Throw {GetCurrentThrowDisplayNumber()}: Player Ready.");
    }

    private void TogglePinsPhysics(bool enable)
    {
        RefreshRuntimePools();
        foreach (var pin in allPinsPool)
        {
            if (pin != null && pin.gameObject.scene.isLoaded)
            {
                pin.SetPhysicsEnabled(enable);
            }
        }
    }

    private void ClearFallenPins()
    {
        RefreshRuntimePools();
        foreach(var p in allPinsPool)
        {
            /*
            if (p.gameObject.scene.isLoaded && p.IsFallen()) 
            {
                p.gameObject.SetActive(false);
            }*/
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
        // Wait for pins to settle
        yield return new WaitForSeconds(7f);

        _currentState = GameState.CalculatingScore;
        Debug.Log("Current State: Calculating Score");

        int totalPinsActiveAtStart = 0;
        int fallenPinsThisThrow = 0;

        RefreshRuntimePools();
        foreach(var p in allPinsPool)
        {
            if (p.gameObject.scene.isLoaded && p.gameObject.activeInHierarchy && CountsTowardFrameClear(p))
            {
                totalPinsActiveAtStart++;
              //  if (p.IsFallen()) fallenPinsThisThrow++;
            }
        }

        AwardVaseIntactBonuses();
        ResetVaseThrowState();

        if (scoreCalculator != null)
        {
            yield return StartCoroutine(scoreCalculator.CalculateScoringSequence());
        }

        bool isStrike = (throwsThisFrame == 1 && fallenPinsThisThrow == totalPinsActiveAtStart && totalPinsActiveAtStart > 0);

        if (isStrike && scoreCalculator != null)
        {
            scoreCalculator.ApplyStrikeBonus();
        }

        int frameScore = 0;
        if (scoreCalculator != null) frameScore = scoreCalculator.GetFinalScore();

        int totalScoreDelta = frameScore - scoreAppliedToTotalThisFrame;
        totalScore += totalScoreDelta;
        scoreAppliedToTotalThisFrame = frameScore;
        Debug.Log($"Tallying Frame! Added {totalScoreDelta} points to total. Frame Total: {frameScore}. Total: {totalScore}");
        
        if (uiController != null) uiController.UpdateStandardUI(this);

        // UI Feedback completed during CalculateScoringSequence

        if (throwsThisFrame >= maxThrowsPerFrame || isStrike)
        {
            bool clearedAllPlacedPins = grid != null && grid.AreAllPlacedPinsCleared();
            bool passedScoreTarget = frameScore >= currentFrameScoreTarget;
            FinalizeFrameTickets(clearedAllPlacedPins, isStrike, frameScore, passedScoreTarget);

            if (!passedScoreTarget)
            {
                _currentState = GameState.GameOver;
                Debug.Log($"Game Over! Missed frame target. Scored {frameScore} / {currentFrameScoreTarget}");
                UpdateInstructionText($"Game Over! Missed the target: {frameScore} / {currentFrameScoreTarget}");
                if (uiController != null) uiController.UpdateStandardUI(this);
            }
            else if (currentFrame >= maxFrames)
            {
                _currentState = GameState.GameOver;
                Debug.Log($"Game Over! Final Score: {totalScore}");
                UpdateInstructionText($"Game Over! Final Score: {totalScore}");
                if (uiController != null) uiController.UpdateStandardUI(this);
            }
            else
            {
                EnterShopPhase();
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
        allPinsPool = FindObjectsOfType<Pin>(true);
        _laneExploders = FindObjectsOfType<exploder>(true);
    }

    private void ResetLaneExploders()
    {
        if (!resetExploderDeformationEachFrame)
        {
            return;
        }

        RefreshRuntimePools();
        foreach (var laneExploder in _laneExploders)
        {
            if (laneExploder != null)
            {
                laneExploder.ResetMesh();
            }
        }
    }

    public void CompleteShopPhase()
    {
        if (_currentState != GameState.Shop)
        {
            return;
        }

        currentFrame++;
        StartFrame();
    }

    public bool TrySpendTickets(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (currentTickets < safeAmount)
        {
            return false;
        }

        currentTickets -= safeAmount;
        if (uiController != null) uiController.UpdateStandardUI(this);
        shopController?.RefreshShop();
        return true;
    }

    public void AddTickets(int amount)
    {
        currentTickets += Mathf.Max(0, amount);
        if (uiController != null) uiController.UpdateStandardUI(this);
        shopController?.RefreshShop();
    }

    public void AddGridTicketsThisFrame(int amount)
    {
        if (amount <= 0)
        {
            return;
        }
        Debug.Log($"<color=orange><b>[GrandGameManager]</b></color> Adding {amount} grid tickets this frame");
        gridTicketsEarnedThisFrame += amount;
        if (uiController != null) uiController.UpdateStandardUI(this);
        shopController?.RefreshShop();
    }

    private void FinalizeFrameTickets(bool clearedAllPlacedPins, bool isStrike, int frameScore, bool passedScoreTarget)
    {
        int targetTickets = 0;
        if (passedScoreTarget && currentFrameScoreTarget > 0)
        {
            targetTickets = Mathf.Max(1, frameScore / currentFrameScoreTarget);
        }

        lastFrameUsedStrikeTicketBonus = passedScoreTarget && isStrike && targetTickets > 0;
        if (lastFrameUsedStrikeTicketBonus)
        {
            targetTickets = Mathf.Max(1, Mathf.RoundToInt(targetTickets * Mathf.Max(1f, strikeTicketMultiplier)));
        }

        scoreTargetTicketsEarnedThisFrame = targetTickets;

        int gridTickets = Mathf.Max(0, gridTicketsEarnedThisFrame);
        lastFrameUsedTicketBonus = clearedAllPlacedPins && gridTickets > 0;
        if (lastFrameUsedTicketBonus)
        {
            gridTickets *= 2;
        }

        ticketsEarnedLastFrame = gridTickets + scoreTargetTicketsEarnedThisFrame;
        currentTickets += ticketsEarnedLastFrame;

        if (uiController != null) uiController.UpdateStandardUI(this);
        shopController?.RefreshShop();

        string bonusText = GetLastFrameTicketBonusSummary();
        string passText = passedScoreTarget
            ? $"Target cleared: {frameScore} / {currentFrameScoreTarget}."
            : $"Target missed: {frameScore} / {currentFrameScoreTarget}.";
        UpdateInstructionText($"Frame complete. {passText} Earned {ticketsEarnedLastFrame} tickets.{bonusText}");
    }

    private void HandlePinFallenForTickets(Pin fallenPin)
    {
        if (_currentState != GameState.Rolling && _currentState != GameState.CalculatingScore)
        {
            return;
        }

        if (grid == null)
        {
            return;
        }

        if (grid.TryCollectTicketForPin(fallenPin, out int collectedValue))
        {
            gridTicketsEarnedThisFrame += collectedValue;
            if (uiController != null) uiController.UpdateStandardUI(this);
            shopController?.RefreshShop();
        }
    }

    // --- INPUT & THROWING LOGIC ---

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (throwsThisFrame == 0 && (_currentState == GameState.PinPlacement || _currentState == GameState.PlayerReady))
            {
                if (_currentState == GameState.PinPlacement)
                {
                    if (!HasPinsPlacedForCurrentFrame())
                    {
                        const string noPinsWarning = "Place at least one pin on the board before bowling.";
                        uiController?.SetPlacementWarning(noPinsWarning);
                        uiController?.UpdateStandardUI(this);
                        UpdateInstructionText(noPinsWarning);
                        return;
                    }

                    uiController?.SetPlacementWarning(string.Empty);
                    ReadyPlayer();
                }
                else EnterPinPlacement();
            }
        }

        if (Input.GetMouseButtonDown(1) && (_currentState == GameState.PlayerReady || _currentState == GameState.Drawing))
        {
            ResetBall();
        }

        /*
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
        */
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
           // powerSlider.value = GetLaunchPowerNormalized(currentDuration);
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
        float t = GetLaunchPowerNormalized(duration);
        float launchSpeed = Mathf.Lerp(forceMinSpeed, forceMaxSpeed, t);
        
        UpdateInstructionText("Ball Released!");
        _currentState = GameState.Rolling; // Instantly lock out further drawing
        Debug.Log($"<b>[GrandGameManager]</b> Launching ball with speed {launchSpeed} and {processedCurve.Count} curve points.");
        StartCoroutine(ThrowSequence(launchSpeed));
    }

    private float GetLaunchPowerNormalized(float drawDuration)
    {
        float taperStartTime = totalMinTime + Mathf.Max(0f, powerFalloffDelay);
        float taperEndTime = Mathf.Max(taperStartTime + 0.0001f, totalMaxTime);

        if (drawDuration <= taperStartTime)
        {
            return 1f;
        }

        return 1f - Mathf.InverseLerp(taperStartTime, taperEndTime, drawDuration);
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
        //if (powerSlider != null) powerSlider.value = 0f;

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
        instructionText.text = newText;
    }

    public void StopFollowingCurve()
    {
        isFollowingCurve = false;
    }

    public int GetCurrentThrowDisplayNumber()
    {
        if (_currentState == GameState.PinPlacement || _currentState == GameState.PlayerReady || _currentState == GameState.Drawing)
        {
            return Mathf.Clamp(throwsThisFrame + 1, 1, Mathf.Max(1, maxThrowsPerFrame));
        }

        return Mathf.Clamp(Mathf.Max(throwsThisFrame, 1), 1, Mathf.Max(1, maxThrowsPerFrame));
    }

    public string GetLastFrameTicketBonusSummary()
    {
        string summary = string.Empty;
        if (lastFrameUsedTicketBonus)
        {
            summary += " Clear bonus x2 applied!";
        }

        if (lastFrameUsedStrikeTicketBonus)
        {
            summary += " Strike ticket bonus x2 applied!";
        }

        return summary;
    }

    private bool HasPinsPlacedForCurrentFrame()
    {
        RefreshRuntimePools();
        for (int i = 0; i < allPinsPool.Length; i++)
        {
            Pin pin = allPinsPool[i];
            if (pin == null || !pin.gameObject.scene.isLoaded || !pin.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (grid != null)
            {
                HexGridSnapper.Cell occupantCell = grid.GetCellByOccupant(pin.transform);
                if (occupantCell != null)
                {
                    return true;
                }
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    private bool CountsTowardFrameClear(Pin pin)
    {
        return pin != null
            && pin.GetComponent<MultPin>() == null
            && pin.GetComponent<TicketPin>() == null
            && pin.GetComponent<VaseEffect>() == null;
    }

    private void AwardVaseIntactBonuses()
    {
        if (scoreCalculator == null)
        {
            return;
        }
        
        /*
        VaseEffect[] vasePins = FindObjectsOfType<VaseEffect>(true);
        for (int i = 0; i < vasePins.Length; i++)
        {
            VaseEffect vase = vasePins[i];
            if (vase == null || !vase.gameObject.scene.isLoaded || !vase.gameObject.activeInHierarchy)
            {
                continue;
            }

            vase.TryAwardIntactBonus(scoreCalculator);
        }
        */
    }

    private void ResetVaseThrowState()
    {
        /*
        VaseEffect[] vasePins = FindObjectsOfType<VaseEffect>(true);
        for (int i = 0; i < vasePins.Length; i++)
        {
            if (vasePins[i] != null)
            {
                vasePins[i].ResetThrowState();
            }
        }
        */
    }

    private int CalculateFrameScoreTarget(int frameNumber)
    {
        if (frameNumber <= 1 || maxFrames <= 1)
        {
            return Mathf.Max(1, Mathf.RoundToInt((baseFrameScoreTarget * Mathf.Max(0.1f, frameScoreTargetGrowthScale)) + flatFrameScoreTargetBonus));
        }

        float normalizedFrame = Mathf.InverseLerp(1f, Mathf.Max(1f, maxFrames), frameNumber);
        float curveValue = Mathf.Max(0.1f, frameScoreTargetCurve.Evaluate(normalizedFrame));
        float scaledTarget = (baseFrameScoreTarget * curveValue * Mathf.Max(0.1f, frameScoreTargetGrowthScale)) + flatFrameScoreTargetBonus;
        return Mathf.Max(1, Mathf.RoundToInt(scaledTarget));
    }

    // --- CAMERA LOGIC ---

    private void UpdateCamera()
    {
        if (targetCamera == null) return;

        bool placementModeActive = (_currentState == GameState.FrameSetup || _currentState == GameState.PinPlacement);
        bool shopModeActive = _currentState == GameState.Shop;
        float targetPlacementBlend = (!shopModeActive && placementModeActive && placementCameraTarget != null) ? 1f : 0f;
        float targetShopBlend = (shopModeActive && shopCameraTarget != null) ? 1f : 0f;
        
        if (!Mathf.Approximately(placementBlend, targetPlacementBlend))
        {
            float duration = Mathf.Max(0.0001f, cameraPlacementTransitionTime);
            placementBlend = Mathf.MoveTowards(placementBlend, targetPlacementBlend, Time.deltaTime / duration);
        }

        if (!Mathf.Approximately(shopBlend, targetShopBlend))
        {
            float duration = Mathf.Max(0.0001f, cameraShopTransitionTime);
            shopBlend = Mathf.MoveTowards(shopBlend, targetShopBlend, Time.deltaTime / duration);
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

        if (shopCameraTarget != null && shopBlend > 0f)
        {
            float smoothShopBlend = shopBlend * shopBlend * (3f - 2f * shopBlend);
            desiredPosition = Vector3.Lerp(desiredPosition, shopCameraTarget.position, smoothShopBlend);
            desiredRotation = Quaternion.Slerp(desiredRotation, shopCameraTarget.rotation, smoothShopBlend);
        }

        targetCamera.transform.position = Vector3.Lerp(targetCamera.transform.position, desiredPosition, cameraSmoothSpeed);
        targetCamera.transform.rotation = Quaternion.Slerp(targetCamera.transform.rotation, desiredRotation, cameraSmoothSpeed);
    }

    private void GetFollowPose(out Vector3 position, out Quaternion rotation)
    {
        float rawFollowZ = ball.transform.position.z + cameraOffset.z;
        if (placementCameraTarget != null)
        {
            rawFollowZ = Mathf.Min(rawFollowZ, placementCameraTarget.position.z);
        }

        Vector3 followPosition = new Vector3(targetCamera.transform.position.x, cameraOffset.y, rawFollowZ);
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
