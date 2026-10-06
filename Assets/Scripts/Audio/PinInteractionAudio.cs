using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent, RequireComponent(typeof(PinBase), typeof(DraggableSnap))]
public sealed class PinInteractionAudio : MonoBehaviour
{
    [SerializeField] private SoundCueSO hover;
    [SerializeField] private SoundCueSO pickup;
    [SerializeField] private SoundCueSO release;

    private PinBase pin;
    private DraggableSnap snap;
    private static EventSystem pointerOwner;
    private static PointerEventData pointerData;
    private static readonly List<RaycastResult> pointerHits = new List<RaycastResult>();

    private void Awake()
    {
        pin = GetComponent<PinBase>();
        snap = GetComponent<DraggableSnap>();
    }

    private bool CanPlay => isActiveAndEnabled && pin != null && pin.isActiveAndEnabled &&
        pin.allowPinSelection && snap != null && snap.CanInteract;

    public void PlayHover()
    {
        if (CanPlay && snap.IsPlaced && DraggableSnap.ActiveDrag == null &&
            (HandManager.Instance == null || !HandManager.Instance.IsAnyCardDragging()) && !IsPointerOverUI())
            GameAudio.Instance?.PlayUIHover(hover);
    }

    public void PlayPickup()
    {
        if (CanPlay && snap.IsDragging) Play(pickup);
    }

    public void PlayRelease()
    {
        if (CanPlay && snap.IsPlaced && !snap.IsDragging) Play(release);
    }

    private void Play(SoundCueSO cue)
    {
        if (cue != null && cue.isUI) GameAudio.Instance?.Play(cue);
    }

    public static bool IsPointerOverUI()
    {
        var current = EventSystem.current;
        if (current == null) return false;
        if (pointerData == null || pointerOwner != current)
        {
            pointerOwner = current;
            pointerData = new PointerEventData(current);
        }

        pointerData.position = Input.mousePosition;
        pointerHits.Clear();
        current.RaycastAll(pointerData, pointerHits);
        bool overUI = false;
        foreach (var hit in pointerHits)
        {
            if (hit.module is UnityEngine.UI.GraphicRaycaster) { overUI = true; break; }
        }
        pointerHits.Clear();
        return overUI;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPointerState()
    {
        pointerOwner = null;
        pointerData = null;
        pointerHits.Clear();
    }
}
