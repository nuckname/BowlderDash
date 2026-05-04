using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MoneyTreePin : MonoBehaviour
{

    [Header("Hit Settings")]
    public string hitterTag = "Player";
    public ScoreCalculator scoreCalculator;
    public int scoreValue = 10;
    public float multValue = 0.5f;


    [Header("Events")]
    public UnityEvent onHit;

    void Start()
    {

    }

    void Update()
    {
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(hitterTag))
            HandleHit(collision.gameObject);
    }


    void HandleHit(GameObject hitter)
    {
        AddScore();
        OnHit(hitter);
    }

    void AddScore()
    {
        if (scoreCalculator != null)
        {
            scoreCalculator.ModifyScore(scoreValue, multValue); // Example: Add scoreValue to score, no multiplier change
        }
    }

    protected virtual void OnHit(GameObject hitter)
    {
        if (onHit != null)
            onHit.Invoke();
    }
}
