using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BossRoundSlot
{
    RoundFour,
    FinalRound
}

[Serializable]
public class BossEncounterOption
{
    public string label = "Boss";
    public BossRoundSlot roundSlot;
    public bool unlocked = true;
    public GameObject prefab;
    public List<BossSpawnArea> spawnAreas = new List<BossSpawnArea>();
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
    public Vector3 floatingTextOffset = new Vector3(0f, 2f, 0f);
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

            manager.MoveCamera(CameraLocation.SpawnBossObjectsCameraPosition);
            SpawnBossObject(manager, selectedOption, selectedArea);
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

    private void SpawnBossObject(RoundStateManager manager, BossEncounterOption option, BossSpawnArea area)
    {
        Vector3 spawnPosition = area.GetRandomPoint();
        Quaternion spawnRotation = area.transform.rotation * Quaternion.Euler(option.rotationOffset);
        spawnedBossObject = UnityEngine.Object.Instantiate(option.prefab, spawnPosition, spawnRotation);
        spawnedBossObject.transform.localScale = GetRandomScale(option);

        if (!string.IsNullOrWhiteSpace(announcementText))
        {
            manager.SpawnBossFloatingText(announcementText, spawnPosition + floatingTextOffset);
        }
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
