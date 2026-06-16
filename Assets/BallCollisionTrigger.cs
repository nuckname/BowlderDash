using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallCollisionTrigger : MonoBehaviour
{
    [SerializeField] private RoundStateManager stateManager;

    private void OnTriggerEnter(Collider other)
    {
        stateManager.OnTriggerEnter(other);
    }
}
