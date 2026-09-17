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

    private IPathDrawListener _iPathDrawListener;

    private List<CurvePoint> _processedCurve = new List<CurvePoint>();
    private float _mouseDownTime;
    private bool _isDrawing = false;
    private bool _isValidDraw = false;
    private float _mouseUpTimer = 0f;

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
            if (_isDrawing) _mouseUpTimer = 0f;
            else StartDrawing();
        }
        else if (Input.GetMouseButton(0) && _isDrawing)
        {
            _mouseUpTimer = 0f;
            UpdateDrawing();
        }
        else if (_isDrawing && !Input.GetMouseButton(0))
        {
            _mouseUpTimer += Time.deltaTime;
            if (_mouseUpTimer >= tuningData.drawTimeTolerance) EndDrawingAndLaunch();
        }
    }

    private void StartDrawing()
    {
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
        _isDrawing = false;
        if (!_isValidDraw || _processedCurve.Count < 2) 
        {
            _iPathDrawListener.OnDrawCancelled();
            return;
        }

        float duration = Time.time - _mouseDownTime;
        _iPathDrawListener.OnDrawCompleted(new List<CurvePoint>(_processedCurve), duration);
    }

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