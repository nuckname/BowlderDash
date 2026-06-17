using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class VaseEffect : PinEffect
{
    [Header("References")]
    public GameObject shatteredVasePrefab;
    public GameObject vase;
    public GameObject flower1;
    public GameObject flower2;

    [Header("Hit Settings")]
    public int intactBonusScore = 10;
    public float flowerShrinkDuration = 0.4f;

    private GameObject shatteredVaseInstance;
    private bool _wasHit;
    private bool _hitThisThrow;
    private bool _hasHandledHitThisFrame;
    private bool _removedFromInventory;

    private Vector3 _flower1StartScale;
    private Vector3 _flower2StartScale;
    private Coroutine _flower1ShrinkCoroutine;
    private Coroutine _flower2ShrinkCoroutine;
    private bool _scalesCached = false;

    private void CacheScalesIfNeeded()
    {
        if (_scalesCached) return;
        CacheFlowerScale(flower1, out _flower1StartScale);
        CacheFlowerScale(flower2, out _flower2StartScale);
        _scalesCached = true;
    }

    public override void Execute(Pin context, Collider instigator = null)
    {
        CacheScalesIfNeeded();

        if (_hasHandledHitThisFrame || _wasHit)
        {
            Debug.Log($"<color=orange><b>[VasePin]</b></color> Collision entered but already handled or was hit");
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.GetCurrentState() == GameState.PinPlacement)
        {
            Debug.Log($"<color=orange><b>[VasePin]</b></color> In pin placement state");
            return;
        }

        // if (collision.gameObject == vase)
        // {
        //     return true;
        // }

        // for (int i = 0; i < collision.contactCount; i++)
        // {
        //     Collider thisCollider = collision.GetContact(i).thisCollider;
        //     if (thisCollider != null && thisCollider.transform.IsChildOf(vase.transform))
        //     {
        //         return true;
        //     }
        // }

        Debug.Log($"<color=orange><b>[VasePin]</b></color> Handling hit");
        HandleHit(context);
    }

    public void TryShatterFromExplosion(Pin context)
    {
        Debug.Log($"<color=orange><b>[VasePin]</b></color> Shattered by nearby explosion");
        TryShatterFromExternalCause(context);
    }

    public void TryShatterFromExternalCause(Pin context)
    {
        if (_wasHit || _removedFromInventory)
        {
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.GetCurrentState() == GameState.PinPlacement)
        {
            return;
        }

        Debug.Log($"<color=orange><b>[VasePin]</b></color> Toppled over");
        HandleHit(context);
    }

    private void HandleHit(Pin context)
    {
        if (_wasHit)
        {
            return;
        }

        _hasHandledHitThisFrame = true;
        _wasHit = true;
        _hitThisThrow = true;

        if (vase != null)
        {
            if (shatteredVasePrefab != null)
            {
                shatteredVaseInstance = UnityEngine.Object.Instantiate(
                    shatteredVasePrefab,
                    vase.transform.position,
                    vase.transform.rotation,
                    context.transform);
            }

            vase.SetActive(false);
        }

        ShrinkFlower(context, flower1, _flower1StartScale, ref _flower1ShrinkCoroutine);
        ShrinkFlower(context, flower2, _flower2StartScale, ref _flower2ShrinkCoroutine);

        if (PinHandInventory.Instance != null && context != null)
        {
            PinHandInventory.Instance.ConsumePlacedVaseInstance(context);
        }
    }

    public bool IsRemovedFromInventory()
    {
        return _removedFromInventory;
    }

    public void MarkRemovedFromInventory()
    {
        _removedFromInventory = true;
    }

    public bool WasHit()
    {
        return _wasHit;
    }

    public bool WasHitThisThrow()
    {
        return _hitThisThrow;
    }

    // This can be called globally at the end of the frame by looping through the Pin's onHitEffects
    public bool TryAwardIntactBonus(Pin context, ScoreCalculator scoreCalculator)
    {
        if (_wasHit || _hitThisThrow || intactBonusScore == 0)
        {
            return false;
        }

        if (scoreCalculator == null)
        {
            return false;
        }

        scoreCalculator.QueueScoreModification(intactBonusScore, 0f, context.transform.position);
        return true;
    }

    public void ResetThrowState()
    {
        _hitThisThrow = false;
        _hasHandledHitThisFrame = false;
    }

    public override void ResetEffect()
    {
        if (_removedFromInventory)
        {
            return;
        }

        _wasHit = false;
        _hitThisThrow = false;
        _hasHandledHitThisFrame = false;

        if (shatteredVaseInstance != null)
        {
            UnityEngine.Object.Destroy(shatteredVaseInstance);
            shatteredVaseInstance = null;
        }

        ApplyIntactVisualState();
        ResetFlower(flower1, _flower1StartScale, ref _flower1ShrinkCoroutine);
        ResetFlower(flower2, _flower2StartScale, ref _flower2ShrinkCoroutine);
    }

    private void ApplyIntactVisualState()
    {
        if (vase != null)
        {
            vase.SetActive(true);
        }

        RestoreFlowerScale(flower1, _flower1StartScale);
        RestoreFlowerScale(flower2, _flower2StartScale);
    }

    private void ShrinkFlower(Pin context, GameObject flower, Vector3 startScale, ref Coroutine runningCoroutine)
    {
        if (flower == null)
        {
            return;
        }

        if (runningCoroutine != null)
        {
            context.StopCoroutine(runningCoroutine);
        }

        runningCoroutine = context.StartCoroutine(ShrinkFlowerRoutine(flower.transform, startScale));
    }

    private IEnumerator ShrinkFlowerRoutine(Transform flowerTransform, Vector3 startScale)
    {
        float duration = Mathf.Max(0.01f, flowerShrinkDuration);
        float elapsed = 0f;
        flowerTransform.localScale = startScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            flowerTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        flowerTransform.localScale = Vector3.zero;
    }

    private void ResetFlower(GameObject flower, Vector3 startScale, ref Coroutine runningCoroutine)
    {
        runningCoroutine = null; 
        RestoreFlowerScale(flower, startScale);
    }

    private static void CacheFlowerScale(GameObject flower, out Vector3 startScale)
    {
        startScale = flower != null ? flower.transform.localScale : Vector3.one;
    }

    private static void RestoreFlowerScale(GameObject flower, Vector3 startScale)
    {
        if (flower == null)
        {
            return;
        }

        flower.transform.localScale = startScale;
    }
}