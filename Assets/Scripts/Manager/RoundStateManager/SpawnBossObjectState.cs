using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BossRoundSlot
{
    RoundFour,
    FinalRound
}

public enum BossSpawnPattern
{
    Single,
    PairedHammers
}

[Serializable]
public class BossEncounterOption
{
    public string label = "Boss";
    public BossRoundSlot roundSlot;
    public bool unlocked = true;
    public GameObject prefab;
    public List<BossSpawnArea> spawnAreas = new List<BossSpawnArea>();
    public BossSpawnPattern spawnPattern;
    [Min(0f)] public float minimumPivotSpacing = 12f;
    [Min(1)] public int placementAttempts = 64;
    public Vector3 minimumScale = Vector3.one;
    public Vector3 maximumScale = Vector3.one;
    public Vector3 rotationOffset = Vector3.zero;

    public bool HasValidSpawnArea()
    {
        if (spawnAreas == null) return false;

        foreach (BossSpawnArea area in spawnAreas)
        {
            if (area != null) return true;
        }

        return false;
    }
}

[Serializable]
public class SpawnBossObjectsState : RoundBaseState
{
    [Header("Boss Choices")]
    public List<BossEncounterOption> bossOptions = new List<BossEncounterOption>();

    [Header("Reveal Settings")]
    public string announcementText = "Boss obstacle incoming!";
    public Transform bossFloatingTextSpawnPoint;
    [Min(0f)] public float revealDuration = 2f;

    private GameObject spawnedBossObject;
    private Coroutine transitionRoutine;
    private int lastActivatedRoundNumber = -1;

    public bool HasSpawnedBoss => spawnedBossObject != null;

    public bool HasRunForRound(int roundNumber)
    {
        return lastActivatedRoundNumber == roundNumber;
    }

    public override void EnterState(RoundStateManager manager)
    {
        int currentRoundNumber = manager.sessionData.currentRound;
        if (!HasRunForRound(currentRoundNumber))
        {
            lastActivatedRoundNumber = currentRoundNumber;
            DespawnActiveBoss();

            BossEncounterOption selectedOption = GetOptionForRound(manager);
            BossSpawnArea selectedArea = selectedOption != null
                ? GetRandomValidEntry(selectedOption.spawnAreas)
                : null;

            if (selectedOption == null || selectedArea == null)
            {
                manager.SwitchState(manager.pinPlacementState);
                return;
            }

            if (!SpawnBossObject(manager, selectedOption, selectedArea))
            {
                manager.SwitchState(manager.pinPlacementState);
                return;
            }

            manager.MoveCamera(CameraLocation.SpawnBossObjectsCameraPosition);
        }
        else if (!HasSpawnedBoss)
        {
            manager.SwitchState(manager.pinPlacementState);
            return;
        }
        else
        {
            manager.MoveCamera(CameraLocation.SpawnBossObjectsCameraPosition);
        }

        transitionRoutine = manager.StartCoroutine(TransitionToPinPlacement(manager));
    }

    private bool SpawnBossObject(RoundStateManager manager, BossEncounterOption option, BossSpawnArea area)
    {
        Quaternion spawnRotation = area.transform.rotation * Quaternion.Euler(option.rotationOffset);

        if (option.spawnPattern == BossSpawnPattern.PairedHammers)
        {
            if (option.prefab.GetComponentInChildren<BossHammerSwingMotor>(true) == null)
            {
                Debug.LogWarning($"Boss choice '{option.label}' needs a HammerUnit prefab with a BossHammerSwingMotor");
                return false;
            }

            if (!area.TryGetRandomPair(option.minimumPivotSpacing, option.placementAttempts,
                    out Vector3 firstPivot, out Vector3 secondPivot))
            {
                Debug.LogWarning($"Boss choice '{option.label}' could not fit two pivots in {area.name} at least {option.minimumPivotSpacing} units apart");
                return false;
            }

            GameObject pairRoot = new GameObject($"Boss - {option.label}");
            SpawnHammer(option.prefab, firstPivot, spawnRotation, pairRoot.transform, true);
            SpawnHammer(option.prefab, secondPivot, spawnRotation, pairRoot.transform, false);
            spawnedBossObject = pairRoot;
        }
        else
        {
            Vector3 spawnPosition = area.GetRandomPoint();
            spawnedBossObject = UnityEngine.Object.Instantiate(option.prefab, spawnPosition, spawnRotation);
            spawnedBossObject.transform.localScale = GetRandomScale(option);
        }

        if (!string.IsNullOrWhiteSpace(announcementText))
        {
            if (bossFloatingTextSpawnPoint != null)
                manager.SpawnBossFloatingText(announcementText, bossFloatingTextSpawnPoint.position);
            else
                Debug.LogWarning("Boss floating text spawn point is not assigned");
        }

        return true;
    }
    
    public void SetBossObjectActive(bool isActive)
    {
        if (spawnedBossObject == null) return;

        foreach (Renderer r in spawnedBossObject.GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = isActive;
        }

        foreach (Collider c in spawnedBossObject.GetComponentsInChildren<Collider>(true))
        {
            c.enabled = isActive;
        }

        foreach (BossHammerSwingMotor motor in spawnedBossObject.GetComponentsInChildren<BossHammerSwingMotor>(true))
        {
            motor.SetSuspended(!isActive);
        }
    }

    private static void SpawnHammer(GameObject prefab, Vector3 pivotPosition, Quaternion rotation,
        Transform pairRoot, bool swingTowardPositiveLimit)
    {
        GameObject hammer = UnityEngine.Object.Instantiate(prefab, pivotPosition, rotation, pairRoot);
        hammer.GetComponentInChildren<BossHammerSwingMotor>(true)
            .SetSwingDirection(swingTowardPositiveLimit);
    }

    private BossEncounterOption GetOptionForRound(RoundStateManager manager)
    {
        if (bossOptions == null) return null;

        int roundNumber = manager.sessionData.currentRound;
        BossRoundSlot targetSlot = manager.roundData.IsLastRound(roundNumber)
            ? BossRoundSlot.FinalRound
            : BossRoundSlot.RoundFour;

        if (targetSlot == BossRoundSlot.RoundFour && roundNumber != 4) return null;

        foreach (BossEncounterOption option in bossOptions)
        {
            if (option == null || option.roundSlot != targetSlot || !option.unlocked)
                continue;

            if (option.prefab == null || !option.HasValidSpawnArea())
            {
                Debug.LogWarning($"Boss choice '{option.label}' needs a prefab and spawn area");
                return null;
            }

            return option;
        }

        return null;
    }

    private static Vector3 GetRandomScale(BossEncounterOption option)
    {
        return new Vector3(
            GetRandomAxisScale(option.minimumScale.x, option.maximumScale.x),
            GetRandomAxisScale(option.minimumScale.y, option.maximumScale.y),
            GetRandomAxisScale(option.minimumScale.z, option.maximumScale.z));
    }

    private static float GetRandomAxisScale(float firstValue, float secondValue)
    {
        return UnityEngine.Random.Range(
            Mathf.Min(firstValue, secondValue),
            Mathf.Max(firstValue, secondValue));
    }

    private static T GetRandomValidEntry<T>(List<T> entries) where T : UnityEngine.Object
    {
        if (entries == null || entries.Count == 0) return null;

        T selectedEntry = null;
        int validEntryCount = 0;

        foreach (T entry in entries)
        {
            if (entry == null) continue;

            validEntryCount++;
            if (UnityEngine.Random.Range(0, validEntryCount) == 0)
                selectedEntry = entry;
        }

        return selectedEntry;
    }

    private IEnumerator TransitionToPinPlacement(RoundStateManager manager)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, revealDuration));

        transitionRoutine = null;

        if (manager.currentState == this)
            manager.SwitchState(manager.pinPlacementState);
    }

    public void DespawnActiveBoss()
    {
        if (spawnedBossObject == null) return;

        UnityEngine.Object.Destroy(spawnedBossObject);
        spawnedBossObject = null;
    }

    public void ResetRuntimeState()
    {
        lastActivatedRoundNumber = -1;
        DespawnActiveBoss();
    }

    public override void UpdateState(RoundStateManager manager) { }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    public override void ExitState(RoundStateManager manager)
    {
        if (transitionRoutine == null) return;

        manager.StopCoroutine(transitionRoutine);
        transitionRoutine = null;
    }
}
