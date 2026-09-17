using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallRampSpawner : MonoBehaviour
{
    [Header("Data References")]
    [SerializeField] private RoundData roundData;
    
    [Header("Spawning Setup")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform spawnPoint; // The empty game object transform to spawn at
    [SerializeField] private float spawnDelay = 0.5f; // Delay to prevent balls clipping inside each other
    
    [Header("Ramp Path")]
    [SerializeField] private List<Transform> rampPath = new List<Transform>();
    
    public void SpawnBallsForRound(int currentRoundNumber)
    {
        RoundScore currentRoundInfo = roundData.GetRoundInfo(currentRoundNumber);
        
        if (currentRoundInfo != null)
        {
            StartCoroutine(SpawnBallsRoutine(currentRoundInfo.maxBallsAllowedToThrow));
        }
        else
        {
            Debug.LogWarning($"Cannot spawn balls: Round {currentRoundNumber} data not found.");
        }
    }

    private IEnumerator SpawnBallsRoutine(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            GameObject spawnedBall = Instantiate(ballPrefab, spawnPoint.position, spawnPoint.rotation);

           BallRampFollower ballFollower = spawnedBall.GetComponent<BallRampFollower>();
          if (ballFollower != null)
          {
               ballFollower.InitializePath(rampPath);
           }

            yield return new WaitForSeconds(spawnDelay);
        }
    }
}