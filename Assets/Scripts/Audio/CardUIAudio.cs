using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent, RequireComponent(typeof(CardMovement))]
public sealed class CardUIAudio : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private SoundCueSO hover;
    [SerializeField] private SoundCueSO click;

    private CardMovement card;
    private HandInteractionManager handInteraction;
    private UnityEngine.UI.Image hitbox;
    private UnityEngine.UI.Selectable selectable;
    private bool pointerInside;
    private readonly List<CanvasGroup> groups = new List<CanvasGroup>();

    private void OnEnable()
    {
        card = GetComponent<CardMovement>();
        hitbox = GetComponent<UnityEngine.UI.Image>();
        selectable = GetComponent<UnityEngine.UI.Selectable>();
        handInteraction = GetComponentInParent<HandInteractionManager>();

        card.PointerEnterEvent?.AddListener(OnPointerEnter);
        card.PointerExitEvent?.AddListener(OnPointerExit);
    }

    private void OnDisable()
    {
        if (card != null)
        {
            card.PointerEnterEvent?.RemoveListener(OnPointerEnter);
            card.PointerExitEvent?.RemoveListener(OnPointerExit);
        }

        pointerInside = false;
    }

    private void OnPointerEnter(CardMovement source)
    {
        if (pointerInside) return;
        pointerInside = true;

        // Returning from a drag can restore the hitbox before the pointer exits.
        if (CanPlay() && !card.wasDragged) GameAudio.Instance?.PlayUIHover(hover);
    }

    private void OnPointerExit(CardMovement source) => pointerInside = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;

        // Listen directly so audio does not depend on CardMovement's drag-only interfaces.
        // BeginDrag, pointer-up and selection must not play another click.
        PlayClick();
    }

    public void PlayKeyboardActivation() => PlayClick();

    private void PlayClick()
    {
        if (CanPlay() && click != null && click.isUI) GameAudio.Instance?.Play(click);
    }

    private bool CanPlay()
    {
        if (!isActiveAndEnabled || card == null || !card.isActiveAndEnabled ||
            card.isDragging || card.isPreviewingInWorld || card.wasSuccessfullyPlayed)
            return false;

        if (hitbox == null || !hitbox.isActiveAndEnabled || !hitbox.raycastTarget)
            return false;

        if (selectable != null && (!selectable.IsActive() || !selectable.IsInteractable()))
            return false;

        // Cards are instantiated before their hand slot is assigned in some flows.
        if (handInteraction == null) handInteraction = GetComponentInParent<HandInteractionManager>();
        if (handInteraction != null &&
            (!handInteraction.isActiveAndEnabled || handInteraction.selectedCardMovement != null))
            return false;

        for (Transform current = transform; current != null; current = current.parent)
        {
            current.GetComponents(groups);
            bool ignoreParents = false;
            foreach (var group in groups)
            {
                if (!group.isActiveAndEnabled) continue;
                if (!group.interactable || !group.blocksRaycasts || group.alpha <= 0f) return false;
                ignoreParents |= group.ignoreParentGroups;
            }

            if (ignoreParents) break;
        }

        return true;
    }
}
