using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallRampSpawner : MonoBehaviour
{
    [Header("Data References")]
    [SerializeField] private RoundData roundData;
    
    [Header("Spawning Setup")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnDelay = 1f; 
    
    private List<Transform> _rampPath = new List<Transform>();

    private void Awake()
    {
        GameObject waypointsParent = GameObject.FindGameObjectWithTag("BallReturnWaypoints");

        if (waypointsParent != null)
        {
            foreach (Transform child in waypointsParent.transform)
            {
                _rampPath.Add(child);
            }
        }
    }

    public void SpawnBallsForRound(int currentRoundNumber)
    {
        RoundScore currentRoundInfo = roundData.GetRoundInfo(currentRoundNumber);
        
        if (currentRoundInfo != null)
        {
            StartCoroutine(SpawnBallsRoutine(currentRoundInfo.maxBallsAllowedToThrow));
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
               ballFollower.InitializePath(_rampPath);
           }

            yield return new WaitForSeconds(spawnDelay);
        }
    }
}