using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpawnBossObjectsState : RoundBaseState
{
    [Header("Boss Settings")]
    public List<GameObject> bossObjectPrefabs = new List<GameObject>();
    public List<BossSpawnArea> bossSpawnAreas = new List<BossSpawnArea>();

    [Header("Scale Settings")]
    public Vector3 minimumScale = Vector3.one;
    public Vector3 maximumScale = Vector3.one;

    [Header("Rotation Settings")]
    public Vector3 rotationOffset = Vector3.zero;

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
        manager.MoveCamera(CameraLocation.SpawnBossObjectsCameraPosition);

        int currentRoundNumber = manager.sessionData.currentRound;
        if (!HasRunForRound(currentRoundNumber))
        {
            lastActivatedRoundNumber = currentRoundNumber;

            if (!HasSpawnedBoss)
            {
                SpawnBossObject(manager);
            }
        }

        if (transitionRoutine != null)
        {
            manager.StopCoroutine(transitionRoutine);
        }

        transitionRoutine = manager.StartCoroutine(TransitionToPinPlacement(manager));
    }

    private void SpawnBossObject(RoundStateManager manager)
    {
        GameObject randomPrefab = GetRandomValidEntry(bossObjectPrefabs);
        BossSpawnArea randomArea = GetRandomValidEntry(bossSpawnAreas);

        if (randomPrefab == null || randomArea == null)
        {
            Debug.LogWarning("Boss phase requires at least one valid prefab and spawn area");
            return;
        }

        Vector3 spawnPosition = randomArea.GetRandomPoint();
        Quaternion spawnRotation = randomArea.transform.rotation * Quaternion.Euler(rotationOffset);
        spawnedBossObject = Object.Instantiate(
            randomPrefab,
            spawnPosition,
            spawnRotation);
        spawnedBossObject.transform.localScale = GetRandomScale();

        if (!string.IsNullOrWhiteSpace(announcementText))
        {
            manager.SpawnBossFloatingText(announcementText, spawnPosition + floatingTextOffset);
        }
    }

    private Vector3 GetRandomScale()
    {
        return new Vector3(
            GetRandomAxisScale(minimumScale.x, maximumScale.x),
            GetRandomAxisScale(minimumScale.y, maximumScale.y),
            GetRandomAxisScale(minimumScale.z, maximumScale.z));
    }

    private static float GetRandomAxisScale(float firstValue, float secondValue)
    {
        return Random.Range(
            Mathf.Min(firstValue, secondValue),
            Mathf.Max(firstValue, secondValue));
    }

    private static T GetRandomValidEntry<T>(List<T> entries) where T : Object
    {
        if (entries == null || entries.Count == 0)
        {
            return null;
        }

        T selectedEntry = null;
        int validEntryCount = 0;

        foreach (T entry in entries)
        {
            if (entry == null)
            {
                continue;
            }

            validEntryCount++;
            if (Random.Range(0, validEntryCount) == 0)
            {
                selectedEntry = entry;
            }
        }

        return selectedEntry;
    }

    private IEnumerator TransitionToPinPlacement(RoundStateManager manager)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, revealDuration));

        transitionRoutine = null;

        if (manager.currentState == this)
        {
            manager.SwitchState(manager.pinPlacementState);
        }
    }

    public void ResetRuntimeState()
    {
        lastActivatedRoundNumber = -1;

        if (spawnedBossObject != null)
        {
            Object.Destroy(spawnedBossObject);
            spawnedBossObject = null;
        }
    }

    public override void UpdateState(RoundStateManager manager) { }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    public override void ExitState(RoundStateManager manager)
    {
        if (transitionRoutine != null)
        {
            manager.StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
    }
}
