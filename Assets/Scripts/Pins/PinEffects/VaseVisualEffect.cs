using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

//Legacy Code
[Serializable]
public class VaseVisualEffect : PinEffect
{
    [Header("References")]
    public GameObject shatteredVasePrefab;
    public GameObject vase;
    public GameObject flower1;
    public GameObject flower2;

    [Header("Hit Settings")]
    public int intactBonusScore = 10;
    public float flowerShrinkDuration = 0.4f;

    private bool _isShattered = false;
    
    private GameObject _shatteredInstance;
    private bool _wasHit;
    private bool _hitThisThrow;
    private bool _hasHandledHitThisFrame;
    private bool _removedFromInventory;

    private Vector3 _flower1StartScale;
    private Vector3 _flower2StartScale;
    private Coroutine _flower1Coroutine;
    private Coroutine _flower2Coroutine;
    private bool _scalesCached = false;
    
    private void CacheScalesIfNeeded()
    {
        if (_scalesCached) return;
        _flower1StartScale = flower1 != null ? flower1.transform.localScale : Vector3.one;
        _flower2StartScale = flower2 != null ? flower2.transform.localScale : Vector3.one;
        _scalesCached = true;
    }

    // Triggered automatically by PinBase when hit (or when falling, depending on which list you put it in)
    public override void Execute(PinBase context, Collider instigator = null)
    {
        if (_isShattered) return;

        CacheScalesIfNeeded();
        _isShattered = true;

        // 1. Swap visual models
        if (vase != null) vase.SetActive(false);
        
        if (shatteredVasePrefab != null && vase != null)
        {
            _shatteredInstance = UnityEngine.Object.Instantiate(
                shatteredVasePrefab, 
                vase.transform.position, 
                vase.transform.rotation, 
                context.transform // Parent to the pin so it cleans up properly
            );
        }

        // 2. Shrink flowers (using the PinBase MonoBehaviour to run the Coroutine)
        ShrinkFlower(context, flower1, _flower1StartScale, ref _flower1Coroutine);
        ShrinkFlower(context, flower2, _flower2StartScale, ref _flower2Coroutine);
    }

    public override void ResetEffect()
    {
        _isShattered = false;

        // 1. Destroy the broken pieces
        if (_shatteredInstance != null)
        {
            UnityEngine.Object.Destroy(_shatteredInstance);
            _shatteredInstance = null;
        }

        // 2. Restore the intact vase model
        if (vase != null) vase.SetActive(true);

        // 3. Restore flowers
        ResetFlower(flower1, _flower1StartScale, ref _flower1Coroutine);
        ResetFlower(flower2, _flower2StartScale, ref _flower2Coroutine);
    }

    // --- Coroutine Helpers ---

    private void ShrinkFlower(PinBase context, GameObject flower, Vector3 startScale, ref Coroutine runningCoroutine)
    {
        if (flower == null) return;
        
        if (runningCoroutine != null) context.StopCoroutine(runningCoroutine);
        
        runningCoroutine = context.StartCoroutine(ShrinkFlowerRoutine(flower.transform, startScale));
    }

    private IEnumerator ShrinkFlowerRoutine(Transform flowerTransform, Vector3 startScale)
    {
        float elapsed = 0f;
        while (elapsed < flowerShrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flowerShrinkDuration);
            flowerTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }
        flowerTransform.localScale = Vector3.zero;
    }

    private void ResetFlower(GameObject flower, Vector3 startScale, ref Coroutine runningCoroutine)
    {
        runningCoroutine = null; 
        if (flower != null) flower.transform.localScale = startScale;
    }
}