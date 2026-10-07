using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// legacy code
// this is from last sem I forgot who wrote it.
public class PathDrawer : MonoBehaviour
{
    public ThrowTuningDataSO tuningData;
    public LayerMask floorLayer;
    public LineRenderer lineRenderer;
    public Slider powerSlider;
    public Camera targetCamera;

    [Header("Drawing Audio")]
    [SerializeField] private SoundCueSO drawingLoopCue;
    [SerializeField, Min(0f)] private float drawingFadeInTime = 0.05f;
    [SerializeField, Min(0f)] private float drawingFadeOutTime = 0.05f;

    private IPathDrawListener _iPathDrawListener;

    private List<CurvePoint> _processedCurve = new List<CurvePoint>();
    private float _mouseDownTime;
    private bool _isDrawing = false;
    private bool _isValidDraw = false;
    private float _mouseUpTimer = 0f;
    private GameAudio.LoopHandle _drawingLoop;
    private bool _drawingAudioRequested;

    [SerializeField] private int drawMaxDistance; 
    
    public void Begin(IPathDrawListener listener)
    {
        _iPathDrawListener = listener;
        ResetDrawing();
    }

    public void Stop()
    {
        _iPathDrawListener = null;
        ResetDrawing();
    }

    public void ResetDrawing()
    {
        StopDrawingAudio();
        CancelInvoke(nameof(EndDrawingAndLaunch));
        _isDrawing = false;
        _isValidDraw = false;
        _processedCurve.Clear();
        if (lineRenderer != null) lineRenderer.positionCount = 0;
        if (powerSlider != null) powerSlider.value = 0f;
        _mouseUpTimer = 0f;
    }
    
    void Update()
    {
        if (_iPathDrawListener == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (_isDrawing)
            {
                _mouseUpTimer = 0f;
                StartDrawingAudio();
            }
            else StartDrawing();
        }
        else if (Input.GetMouseButton(0) && _isDrawing)
        {
            _mouseUpTimer = 0f;
            UpdateDrawing();
        }
        else if (_isDrawing && !Input.GetMouseButton(0))
        {
            PauseDrawingAudio();
            _mouseUpTimer += Time.deltaTime;
            if (_mouseUpTimer >= tuningData.drawTimeTolerance) EndDrawingAndLaunch();
        }
    }

    private void StartDrawing()
    {
        StopDrawingAudio();
        CancelInvoke(nameof(EndDrawingAndLaunch));
        _processedCurve.Clear();
        if (lineRenderer != null) lineRenderer.positionCount = 0;
        _mouseUpTimer = 0f;

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, drawMaxDistance, floorLayer))
        {
            if (hit.point.z < tuningData.minZ)
            {
                _isValidDraw = true;
                _isDrawing = true;
                _mouseDownTime = Time.time;
                AddPoint(hit.point);
                StartDrawingAudio();
                _iPathDrawListener.OnInstructionUpdated("Drawing path... Release to bowl!");
            }
            else
            {
                _isValidDraw = false;
                _isDrawing = false;
                _iPathDrawListener.OnInstructionUpdated("Draw from the ball!");
            }
        }
    }

    private void UpdateDrawing()
    {
        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, drawMaxDistance, floorLayer))
        {
            Vector3 point = hit.point;

            if (_processedCurve.Count > 3 && point.z < _processedCurve[_processedCurve.Count - 1].position.z - tuningData.drawPositionTolerance)
            {
                EndDrawingAndLaunch();
                return;
            }

            if (point.z >= tuningData.maxZ)
            {
                _isDrawing = false;
                StopDrawingAudio();
                Invoke(nameof(EndDrawingAndLaunch), 0.3f);
                return;
            }

            if (_processedCurve.Count == 0 || point.z > _processedCurve[_processedCurve.Count - 1].position.z + 0.1f)
            {
                AddPoint(point);
            }
        }
        
        if (powerSlider != null && _isValidDraw)
        {
            float currentDuration = Time.time - _mouseDownTime;
            powerSlider.value = GetLaunchPowerNormalized(currentDuration);
        }
    }
    
    private void EndDrawingAndLaunch()
    {
        StopDrawingAudio();
        _isDrawing = false;
        if (_iPathDrawListener == null) return;
        if (!_isValidDraw || _processedCurve.Count < 2) 
        {
            _iPathDrawListener.OnDrawCancelled();
            return;
        }

        float duration = Time.time - _mouseDownTime;
        _iPathDrawListener.OnDrawCompleted(new List<CurvePoint>(_processedCurve), duration);
    }

    // Request once per held segment; unavailable audio must not be retried every frame.
    private void StartDrawingAudio()
    {
        if (_drawingAudioRequested) return;
        _drawingAudioRequested = true;
        if (_drawingLoop.IsValid)
        {
            _drawingLoop.SetParameters(1f, 1f, drawingFadeInTime);
        }
        else if (GameAudio.Instance != null)
        {
            _drawingLoop = GameAudio.Instance.BeginLoop(drawingLoopCue, this, fadeInTime: drawingFadeInTime);
        }
    }

    // Keep the handle through release tolerance so completion can still stop it immediately.
    private void PauseDrawingAudio()
    {
        if (!_drawingAudioRequested) return;
        _drawingAudioRequested = false;
        _drawingLoop.SetParameters(0f, 1f, drawingFadeOutTime);
    }

    private void StopDrawingAudio()
    {
        _drawingAudioRequested = false;
        _drawingLoop.Stop(0f);
        _drawingLoop = default;
    }

    // Disabling the drawer also cancels its delayed maximum-distance launch.
    private void OnDisable() => ResetDrawing();

    private float GetLaunchPowerNormalized(float drawDuration)
    {
        float taperStartTime = tuningData.totalMinTime + Mathf.Max(0f, tuningData.powerFalloffDelay);
        float taperEndTime = Mathf.Max(taperStartTime + 0.0001f, tuningData.totalMaxTime);

        if (drawDuration <= taperStartTime) return 1f;
        return 1f - Mathf.InverseLerp(taperStartTime, taperEndTime, drawDuration);
    }

    private void AddPoint(Vector3 pos)
    {
        CurvePoint cp = new CurvePoint { position = pos, directionIndex = 0 };
        
        if (_processedCurve.Count > 0)
        {
            CurvePoint last = _processedCurve[_processedCurve.Count - 1];
            float currentDelta = pos.x - last.position.x;
            int currentDirSign = (Mathf.Abs(currentDelta) > tuningData.jitterTolerance) ? (int)Mathf.Sign(currentDelta) : 0;
            
            int lastDirSign = 0;
            for (int i = _processedCurve.Count - 1; i >= 1; i--)
            {
                float d = _processedCurve[i].position.x - _processedCurve[i-1].position.x;
                if (Mathf.Abs(d) > tuningData.jitterTolerance)
                {
                    lastDirSign = (int)Mathf.Sign(d);
                    break;
                }
            }

            cp.directionIndex = last.directionIndex;
            if (currentDirSign != 0 && lastDirSign != 0 && currentDirSign != lastDirSign) cp.directionIndex++;
        }

        _processedCurve.Add(cp);

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = _processedCurve.Count;
            lineRenderer.SetPosition(_processedCurve.Count - 1, pos + Vector3.up * 0.05f);
        }
    }
}
