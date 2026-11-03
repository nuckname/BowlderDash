using UnityEngine;

public class CursorHoverTarget : MonoBehaviour
{
    private void OnMouseEnter()
    {
        if (GameplayPause.IsInputBlocked(this)) return;
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetHoverState(true);
        }
    }

    private void OnMouseExit()
    {
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetHoverState(false);
        }
    }

    private void OnDisable()
    {
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetHoverState(false);
        }
    }
}