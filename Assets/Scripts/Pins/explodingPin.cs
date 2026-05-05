using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class explodingPin : MonoBehaviour
{
    public exploder targetExploder;
    
    public float explosionForce = 15f;
    public float explosionRadius = 5f;
    
    public float upwardModifier = 1.0f;
    public ParticleSystem explosionParticles;


    public float tipAngleThreshold = 30f;
    public float detonationDelay = 1.0f;

    private bool _isKnockedDown = false;
    private bool _hasExploded = false;
    private Rigidbody _rb;
    private Coroutine _detonationCoroutine;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        if (targetExploder == null)
        {
            targetExploder = Object.FindAnyObjectByType<exploder>();
        }
    }

    private void Update()
    {
        if (_hasExploded || _isKnockedDown) return;

        float angle = Vector3.Angle(transform.up, Vector3.up);
        if (angle > tipAngleThreshold)
        {
            TriggerKnockdown();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hasExploded || _isKnockedDown) return;

        if (collision.relativeVelocity.magnitude > 3f)
        {
            if (Vector3.Angle(transform.up, Vector3.up) > 10f)
            {
                TriggerKnockdown();
            }
        }
    }

    private void TriggerKnockdown()
    {
        if (_isKnockedDown) return;
        _isKnockedDown = true;
        
        _detonationCoroutine = StartCoroutine(DetonationSequence());
    }

    private IEnumerator DetonationSequence()
    {
        yield return new WaitForSeconds(detonationDelay);
        _detonationCoroutine = null;

        if (!_isKnockedDown || _hasExploded)
        {
            yield break;
        }

        Detonate();
    }

    private void Detonate()
    {
        if (_hasExploded) return;
        _hasExploded = true;

        Vector3 explosionPos = transform.position;

        Collider[] colliders = Physics.OverlapSphere(explosionPos, explosionRadius);
        foreach (Collider hit in colliders)
        {
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null && rb != _rb) //avoid self blow
            {
                rb.AddExplosionForce(explosionForce, explosionPos, explosionRadius, upwardModifier, ForceMode.Impulse);
            }
        }

        if (targetExploder != null)
        {
            targetExploder.AddExplosionPoint(explosionPos);
        }

        CreateExplosionFeedback();
    }

    private void CreateExplosionFeedback()
    {
        Debug.Log($"<color=orange><b>[ExplodingPin]</b></color> Detonated at {transform.position}");
        GetComponent<Renderer>().enabled = false;
        _rb.isKinematic = true;

        if (explosionParticles != null)
        {
            explosionParticles.Play();
        }

    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawSphere(transform.position, explosionRadius);
    }

    public void ResetPin()
    {
        if (_detonationCoroutine != null)
        {
            StopCoroutine(_detonationCoroutine);
            _detonationCoroutine = null;
        }

        _isKnockedDown = false;
        _hasExploded = false;
        GetComponent<Renderer>().enabled = true;
        if (_rb != null) _rb.isKinematic = false;

        if (explosionParticles != null)
        {
            explosionParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

    }
}
