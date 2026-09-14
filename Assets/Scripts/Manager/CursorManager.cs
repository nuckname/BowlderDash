using UnityEngine;
using System.Collections;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Cursor Textures")]
    public Texture2D defaultCursor;
    public Texture2D clickCursor;
    public Texture2D hoverCursor;

    [Header("Cursor Settings")]
    public Vector2 hotspot = Vector2.zero;

    [Header("Debug")]
    public bool showDebugHotspot = true;

    private bool isHovering = false;
    private bool isClickFlashing = false;

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
            StartCoroutine(FlashClickRoutine());
        }
        else if (Input.GetMouseButtonUp(0))
        {
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
        isClickFlashing = true;
        SetCursor(clickCursor);
        
        yield return new WaitForSeconds(0.1f);
        
        isClickFlashing = false;
        UpdateCursorVisual();
    }

    private void UpdateCursorVisual()
    {
        if (isClickFlashing) return;
        SetCursor(isHovering ? hoverCursor : defaultCursor);
    }

    private void SetCursor(Texture2D cursorTexture)
    {
        Cursor.SetCursor(cursorTexture, hotspot, CursorMode.Auto);
    }

    private void OnGUI()
    {
        if (showDebugHotspot)
        {
            Event currentEvent = Event.current;
            Vector2 mousePos = currentEvent.mousePosition;
            
            GUI.color = Color.red;
            GUI.DrawTexture(new Rect(mousePos.x, mousePos.y, 4, 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}