using UnityEngine;
using System.Collections;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Cursor Textures")]
    public Texture2D defaultCursor;
    public Texture2D clickCursor;
    public Texture2D hoverCursor;
    public Texture2D grabCursor;

    [Header("Cursor Settings")]
    public Vector2 hotspot = Vector2.zero;
    public LayerMask pinLayer;

    [Header("Debug")]
    public bool showDebugHotspot = true;

    private bool isHovering = false;
    private bool isClickFlashing = false;
    private bool isGrabbing = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        SetCursor(defaultCursor);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, pinLayer))
            {
                isGrabbing = true;
                UpdateCursorVisual();
            }
            else
            {
                StartCoroutine(FlashClickRoutine());
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isGrabbing = false;
            UpdateCursorVisual();
        }
    }

    public void SetHoverState(bool state)
    {
        isHovering = state;
        UpdateCursorVisual();
    }

    private IEnumerator FlashClickRoutine()
    {
        if (isGrabbing) yield break;

        isClickFlashing = true;
        SetCursor(clickCursor);
        
        yield return new WaitForSeconds(0.1f);
        
        isClickFlashing = false;
        UpdateCursorVisual();
    }

    private void UpdateCursorVisual()
    {
        if (isGrabbing)
        {
            SetCursor(grabCursor);
            return;
        }

        if (isClickFlashing) return;
        
        SetCursor(isHovering ? hoverCursor : defaultCursor);
    }

    private void SetCursor(Texture2D cursorTexture)
    {
        Cursor.SetCursor(cursorTexture, hotspot, CursorMode.Auto);
    }
}