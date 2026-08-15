using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpawnBossObjectsState : RoundBaseState
{
    [Header("Boss Settings")]
    public List<GameObject> bossObjectPrefabs = new List<GameObject>();
    public List<Transform> bossSpawnLocations = new List<Transform>();

    public override void EnterState(RoundStateManager manager)
    {
        manager.MoveCamera(CameraLocation.SpawnBossObjectsCameraPosition);

        if (bossSpawnLocations.Count > 0 && bossObjectPrefabs.Count > 0)
        {
            Transform randomSpawn = bossSpawnLocations[Random.Range(0, bossSpawnLocations.Count)];
            GameObject randomPrefab = bossObjectPrefabs[Random.Range(0, bossObjectPrefabs.Count)];
            
            Object.Instantiate(randomPrefab, randomSpawn.position, randomSpawn.rotation);
        }
    }

    public override void UpdateState(RoundStateManager manager) { }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    public override void ExitState(RoundStateManager manager) { }
}