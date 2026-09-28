using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class UIButtonAudio : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
{
    [SerializeField] private SoundCueSO hover;
    [SerializeField] private SoundCueSO click;
    private Button button;
    private bool pointerInside;
    private readonly List<CanvasGroup> groups = new List<CanvasGroup>();

    private bool CanPlay()
    {
        if (button == null) button = GetComponent<Button>();
        return isActiveAndEnabled && button != null && button.IsActive() && button.IsInteractable();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        if (CanPlay() && AllowsPointer()) GameAudio.Instance?.PlayUIHover(hover);
    }

    public void OnPointerExit(PointerEventData eventData) => pointerInside = false;

    public void OnSelect(BaseEventData eventData)
    {
        // Mouse press selects an already-hovered button; it is not a second hover.
        if (!pointerInside && CanPlay()) GameAudio.Instance?.PlayUIHover(hover);
    }

    // Authored as the FIRST persistent Button.onClick callback, before gameplay
    // callbacks (including runtime listeners). Do not also bind pointer-down/submit.
    public void PlayClick()
    {
        if (CanPlay()) GameAudio.Instance?.Play(click);
    }

    private bool AllowsPointer()
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            current.GetComponents(groups);
            bool ignoreParents = false;
            foreach (var group in groups)
            {
                if (!group.isActiveAndEnabled) continue;
                if (!group.blocksRaycasts) return false;
                ignoreParents |= group.ignoreParentGroups;
            }
            if (ignoreParents) break;
        }
        return true;
    }

    private void OnDisable() => pointerInside = false;
}
