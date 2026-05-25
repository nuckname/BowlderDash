using System.Collections;
using UnityEngine;

public class vasePin : MonoBehaviour
{
    [Header("References")]
    public GameObject shatteredVasePrefab;
    public GameObject vase;
    public GameObject flower1;
    public GameObject flower2;

    [Header("Hit Settings")]
    public string hitterTag = "Ball";
    public int intactBonusScore = 10;
    public float flowerShrinkDuration = 0.4f;
    public float tipAngleThreshold = 65f;

    private GameObject shatteredVaseInstance;
    private bool _wasHit;
    private bool _hitThisThrow;
    private bool _hasHandledHitThisFrame;
    private bool _removedFromInventory;

    private Vector3 _flower1StartScale;
    private Vector3 _flower2StartScale;
    private Coroutine _flower1ShrinkCoroutine;
    private Coroutine _flower2ShrinkCoroutine;

    private void Awake()
    {
        CacheFlowerScale(flower1, out _flower1StartScale);
        CacheFlowerScale(flower2, out _flower2StartScale);

        Pin pin = GetComponent<Pin>();
        if (pin != null)
        {
            pin.score = 0;
            pin.mult = 0;
            pin.fallAngleThreshold = 180f;
        }

        ApplyIntactVisualState();
    }

    private void Update()
    {
        if (GrandGameManager.Instance != null
            && GrandGameManager.Instance.GetCurrentState() == GameState.PinPlacement)
        {
            _hasHandledHitThisFrame = false;
            return;
        }

        if (!_wasHit && Vector3.Angle(transform.up, Vector3.up) > tipAngleThreshold)
        {
            Debug.Log($"<color=orange><b>[VasePin]</b></color> Toppled over");
            TryShatterFromExternalCause();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hasHandledHitThisFrame || _wasHit)
        {
            Debug.Log($"<color=orange><b>[VasePin]</b></color> Collision entered but already handled or was hit");
            return;
        }

        if (GrandGameManager.Instance != null
            && GrandGameManager.Instance.GetCurrentState() == GameState.PinPlacement)
        {
            Debug.Log($"<color=orange><b>[VasePin]</b></color> In pin placement state");
            return;
        }

        if (!IsValidHitter(collision.gameObject))
        {
            Debug.Log($"<color=orange><b>[VasePin]</b></color> Invalid hitter");
            return;
        }

        if (!IsVaseCollision(collision))
        {
            Debug.Log($"<color=orange><b>[VasePin]</b></color> Not a vase collision");
            return;
        }

        Debug.Log($"<color=orange><b>[VasePin]</b></color> Handling hit");
        HandleHit();
    }

    private bool IsValidHitter(GameObject hitter)
    {
        if (hitter == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(hitterTag) && hitter.CompareTag(hitterTag))
        {
            return true;
        }

        GrandGameManager gameManager = GrandGameManager.Instance;
        return gameManager != null && gameManager.ball == hitter;
    }

    private bool IsVaseCollision(Collision collision)
    {
        if (vase == null)
        {
            return false;
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

        return true;
    }

    public void TryShatterFromExplosion()
    {
        Debug.Log($"<color=orange><b>[VasePin]</b></color> Shattered by nearby explosion");
        TryShatterFromExternalCause();
    }

    public void TryShatterFromExternalCause()
    {
        if (_wasHit || _removedFromInventory)
        {
            return;
        }

        if (GrandGameManager.Instance != null
            && GrandGameManager.Instance.GetCurrentState() == GameState.PinPlacement)
        {
            return;
        }

        HandleHit();
    }

    private void HandleHit()
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
                shatteredVaseInstance = Instantiate(
                    shatteredVasePrefab,
                    vase.transform.position,
                    vase.transform.rotation,
                    transform);
            }

            vase.SetActive(false);
        }

        ShrinkFlower(flower1, _flower1StartScale, ref _flower1ShrinkCoroutine);
        ShrinkFlower(flower2, _flower2StartScale, ref _flower2ShrinkCoroutine);

        Pin pin = GetComponent<Pin>();
        if (PinHandInventory.Instance != null && pin != null)
        {
            PinHandInventory.Instance.ConsumePlacedVaseInstance(pin);
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

    public bool TryAwardIntactBonus(ScoreCalculator scoreCalculator)
    {
        if (_wasHit || _hitThisThrow || intactBonusScore == 0)
        {
            return false;
        }

        if (scoreCalculator == null)
        {
            return false;
        }

        scoreCalculator.QueueScoreModification(intactBonusScore, 0f, transform.position);
        return true;
    }

    public void ResetThrowState()
    {
        _hitThisThrow = false;
        _hasHandledHitThisFrame = false;
    }

    public void ResetPin()
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
            Destroy(shatteredVaseInstance);
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

    private void ShrinkFlower(GameObject flower, Vector3 startScale, ref Coroutine runningCoroutine)
    {
        if (flower == null)
        {
            return;
        }

        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
        }

        runningCoroutine = StartCoroutine(ShrinkFlowerRoutine(flower.transform, startScale));
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
        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }

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
